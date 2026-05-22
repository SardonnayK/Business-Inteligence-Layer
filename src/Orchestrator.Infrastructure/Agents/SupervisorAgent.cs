using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Orchestrator.Core.Entities;
using Orchestrator.Core.Enums;
using Orchestrator.Core.Interfaces;
using Orchestrator.Infrastructure.Data;
using Orchestrator.Infrastructure.Services;

namespace Orchestrator.Infrastructure.Agents;

public record SupervisorConfig(
    bool RequireConfirmationForRerouting,
    bool AutoIngestAgentOutput,
    bool AllowAutoCreateArtifacts)
{
    public static SupervisorConfig Default => new(false, true, true);
}

public class SupervisorAgent : ISupervisorAgent
{
    private readonly AppDbContext _db;
    private readonly IHybridRagService _ragService;
    private readonly IChatProviderFactory _chatFactory;
    private readonly IEmbeddingProviderFactory _embeddingFactory;
    private readonly ITextExtractor _textExtractor;
    private readonly PendingConfirmationStore _pendingStore;
    private readonly IServiceProvider _serviceProvider;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMcpClientService _mcpClientService;

    public SupervisorAgent(
        AppDbContext db,
        IHybridRagService ragService,
        IChatProviderFactory chatFactory,
        IEmbeddingProviderFactory embeddingFactory,
        ITextExtractor textExtractor,
        PendingConfirmationStore pendingStore,
        IServiceProvider serviceProvider,
        IHttpClientFactory httpClientFactory,
        IMcpClientService mcpClientService)
    {
        _db = db;
        _ragService = ragService;
        _chatFactory = chatFactory;
        _embeddingFactory = embeddingFactory;
        _textExtractor = textExtractor;
        _pendingStore = pendingStore;
        _serviceProvider = serviceProvider;
        _httpClientFactory = httpClientFactory;
        _mcpClientService = mcpClientService;
    }

    public async Task<SupervisorResult> ProcessAsync(SupervisorRequest request, CancellationToken ct)
    {
        var tenantId = request.TenantId;

        // 1. Load AgentRegistration
        AgentRegistration? registration;

        if (request.ForceRegistrationId.HasValue)
        {
            registration = await _db.AgentRegistrations
                .FirstOrDefaultAsync(r => r.Id == request.ForceRegistrationId.Value, ct);
        }
        else
        {
            registration = await _db.AgentRegistrations
                .Where(r => (r.TenantId == tenantId || r.TenantId == null)
                         && r.Capability == request.Capability
                         && r.IsEnabled)
                .OrderByDescending(r => r.TenantId != null)
                .ThenByDescending(r => r.Priority)
                .FirstOrDefaultAsync(ct);
        }

        // 2. Load SupervisorConfig from Tenant.AgentConfigJson
        var tenant = await _db.Tenants.AsNoTracking().FirstAsync(t => t.Id == request.TenantId, ct);
        var config = string.IsNullOrEmpty(tenant.AgentConfigJson)
            ? SupervisorConfig.Default
            : JsonSerializer.Deserialize<SupervisorConfig>(tenant.AgentConfigJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
              ?? SupervisorConfig.Default;

        var executionId = Guid.NewGuid();

        // 3. For Query and Discover: handle directly in supervisor
        if (request.Capability == AgentCapability.Discover)
        {
            var noBuiltIn = registration is null || registration.AgentType != AgentType.BuiltIn;
            if (noBuiltIn || _serviceProvider.GetKeyedService<IAgent>(AgentCapability.Discover) is null)
                return await HandleDiscoverAsync(request, executionId, ct);
        }

        if (request.Capability == AgentCapability.Query)
        {
            var noBuiltIn = registration is null || registration.AgentType != AgentType.BuiltIn;
            if (noBuiltIn || _serviceProvider.GetKeyedService<IAgent>(AgentCapability.Query) is null)
                return await HandleQueryAsync(request, executionId, ct);
        }

        // 4. Resolve worker IAgent for Ingest / General (and any capability with a registered worker)
        IAgent? worker = null;

        if (registration?.AgentType == AgentType.BuiltIn)
        {
            worker = _serviceProvider.GetKeyedService<IAgent>(registration.Capability);
        }
        else if (registration?.AgentType == AgentType.HttpPlugin)
        {
            worker = new HttpPluginAgent(registration, _httpClientFactory);
        }

        if (worker is null)
        {
            return new SupervisorResult(
                Success: false,
                Output: $"No worker agent found for capability '{request.Capability}'.",
                RequiresUserConfirmation: false,
                ConfirmationMessage: null,
                SuggestedArtifactId: null,
                RoutedArtifactId: null,
                IngestedItems: [],
                AgentName: registration?.Name ?? "Unknown",
                ExecutionId: executionId);
        }

        // 5. Fetch context per worker.RequiredContext
        var ragTopK = worker.Capability == AgentCapability.General ? 10 : 5;
        var agentContext = await BuildContextAsync(worker.RequiredContext, tenantId, request.TextInput, ct, ragTopK);

        // 6. Build AgentRequest and call worker
        var agentRequest = new AgentRequest(
            ExecutionId: executionId,
            TenantId: tenantId,
            RequestType: request.Capability,
            TextInput: request.TextInput,
            FileBytes: request.FileBytes,
            FileName: request.FileName,
            MimeType: request.MimeType,
            HintArtifactId: request.HintArtifactId,
            Context: agentContext);

        var agentResult = await worker.ExecuteAsync(agentRequest, ct);

        // 7. Post-execution SAGA step
        if (agentResult.RequiresUserConfirmation && config.RequireConfirmationForRerouting)
        {
            _pendingStore.Add(executionId, new PendingConfirmation(agentRequest, agentResult, worker.Capability.ToString()));
            return new SupervisorResult(
                Success: true,
                Output: agentResult.ConfirmationMessage,
                RequiresUserConfirmation: true,
                ConfirmationMessage: agentResult.ConfirmationMessage,
                SuggestedArtifactId: agentResult.SuggestedArtifactId,
                RoutedArtifactId: agentResult.RoutedArtifactId,
                IngestedItems: [],
                AgentName: registration?.Name ?? worker.Capability.ToString(),
                ExecutionId: executionId);
        }

        IReadOnlyList<IngestItem> ingestedItems = [];
        if (agentResult.IngestItems.Count > 0 && config.AutoIngestAgentOutput
            && !agentResult.RequiresUserConfirmation && !request.SkipAutoIngest)
        {
            await ExecuteIngestItemsAsync(agentResult.IngestItems, request, agentResult.RoutedArtifactId, ct);
            ingestedItems = agentResult.IngestItems;
        }

        return new SupervisorResult(
            Success: agentResult.Success,
            Output: agentResult.Output,
            RequiresUserConfirmation: false,
            ConfirmationMessage: null,
            SuggestedArtifactId: agentResult.SuggestedArtifactId,
            RoutedArtifactId: agentResult.RoutedArtifactId,
            IngestedItems: ingestedItems,
            AgentName: registration?.Name ?? worker.Capability.ToString(),
            ExecutionId: executionId);
    }

    public async Task<SupervisorResult> ConfirmAsync(Guid executionId, bool accept, CancellationToken ct)
    {
        if (!_pendingStore.TryRemove(executionId, out var pending) || pending is null)
        {
            return new SupervisorResult(
                Success: false,
                Output: $"No pending confirmation found for execution '{executionId}'.",
                RequiresUserConfirmation: false,
                ConfirmationMessage: null,
                SuggestedArtifactId: null,
                RoutedArtifactId: null,
                IngestedItems: [],
                AgentName: "Unknown",
                ExecutionId: executionId);
        }

        var artifactId = accept
            ? pending.AgentResult.SuggestedArtifactId
            : pending.OriginalRequest.HintArtifactId;

        var text = pending.OriginalRequest.TextInput;
        if (!string.IsNullOrWhiteSpace(text) && artifactId.HasValue)
        {
            await _ragService.IngestAsync(
                text,
                pending.OriginalRequest.TenantId,
                artifactId,
                source: null,
                category: null,
                ct: ct);
        }

        return new SupervisorResult(
            Success: true,
            Output: accept ? "Ingested to suggested artifact." : "Ingested to original artifact.",
            RequiresUserConfirmation: false,
            ConfirmationMessage: null,
            SuggestedArtifactId: pending.AgentResult.SuggestedArtifactId,
            RoutedArtifactId: artifactId,
            IngestedItems: pending.AgentResult.IngestItems,
            AgentName: pending.AgentName,
            ExecutionId: executionId);
    }

    // ── Direct-handle: Discover ──────────────────────────────────────────────

    private async Task<SupervisorResult> HandleDiscoverAsync(SupervisorRequest request, Guid executionId, CancellationToken ct)
    {
        var tenantId = request.TenantId;

        var texts = await _db.BusinessContexts
            .AsNoTracking()
            .Where(bc => bc.TenantId == tenantId)
            .Take(30)
            .Select(bc => bc.Text)
            .ToListAsync(ct);

        var providerConfig = await GetProviderConfigAsync(tenantId, ct);
        IChatClient? chatClient = providerConfig is not null ? _chatFactory.GetChatClient(providerConfig) : null;

        if (chatClient is null)
        {
            var (dept, artifact, sharedArtifact) = await CreateGeneralDepartmentAsync(tenantId, ct);
            await _db.SaveChangesAsync(ct);

            var deptCount = dept is not null ? 1 : 0;
            var artCount = (artifact is not null ? 1 : 0) + (sharedArtifact is not null ? 1 : 0);
            return new SupervisorResult(
                Success: true,
                Output: $"Discovered {deptCount} departments and {artCount} artifacts.",
                RequiresUserConfirmation: false,
                ConfirmationMessage: null,
                SuggestedArtifactId: null,
                RoutedArtifactId: null,
                IngestedItems: [],
                AgentName: "Supervisor Agent",
                ExecutionId: executionId);
        }

        var manifest = await LoadManifestAsync(tenantId, ct);
        var sampleText = string.Join("\n---\n", texts);

        var sizingGuidance = manifest is not null
            ? "Use this tenant department manifest to guide department naming and artifact sizing:\n" + manifest.Content + "\n\n"
            : "For each department specify how many artifacts (knowledge documents) it needs based on complexity:\n" +
              "- small (focused): 1 artifact\n" +
              "- medium: 2 artifacts\n" +
              "- large (broad): 3 artifacts\n\n";

        var prompt =
            "Analyze these knowledge base text samples and identify the organizational departments.\n" +
            sizingGuidance +
            "Samples:\n" + sampleText + "\n\n" +
            "Reply with ONLY valid JSON:\n" +
            "{\"departments\":[{\"name\":\"Engineering\",\"description\":\"Technical knowledge and processes\",\"estimatedSize\":\"large\",\"artifacts\":[{\"name\":\"Technical Standards\",\"description\":\"Coding and architecture guidelines\"},{\"name\":\"Engineering Processes\",\"description\":\"Deployment, incidents, sprint rituals\"}]}],\"sharedArtifact\":{\"name\":\"Company Knowledge\",\"description\":\"Mission, values, strategy, cross-departmental policies\"}}";

        var createdDepartments = new List<Department>();
        var createdArtifacts = new List<Artifact>();

        try
        {
            var response = await chatClient.GetResponseAsync(prompt, cancellationToken: ct);
            var responseText = response.Text;

            if (!string.IsNullOrWhiteSpace(responseText))
            {
                using var doc = JsonDocument.Parse(responseText.Trim());
                var root = doc.RootElement;

                if (root.TryGetProperty("departments", out var departmentsEl) &&
                    departmentsEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var deptEl in departmentsEl.EnumerateArray())
                    {
                        var deptName = deptEl.TryGetProperty("name", out var np) ? np.GetString() ?? string.Empty : string.Empty;
                        var deptDesc = deptEl.TryGetProperty("description", out var dp) ? dp.GetString() ?? string.Empty : string.Empty;
                        var estimatedSize = deptEl.TryGetProperty("estimatedSize", out var sp) ? sp.GetString() ?? "small" : "small";

                        if (string.IsNullOrWhiteSpace(deptName)) continue;

                        var existingDept = await _db.Departments
                            .FirstOrDefaultAsync(d => d.TenantId == tenantId && d.Name == deptName, ct);

                        Department department;
                        if (existingDept is null)
                        {
                            department = new Department
                            {
                                Id = Guid.NewGuid(),
                                TenantId = tenantId,
                                Name = deptName,
                                Description = deptDesc,
                                EstimatedSize = estimatedSize,
                                CreatedAt = DateTime.UtcNow
                            };
                            _db.Departments.Add(department);
                            createdDepartments.Add(department);
                        }
                        else
                        {
                            department = existingDept;
                        }

                        if (deptEl.TryGetProperty("artifacts", out var artifactsEl) &&
                            artifactsEl.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var artEl in artifactsEl.EnumerateArray())
                            {
                                var artName = artEl.TryGetProperty("name", out var an) ? an.GetString() ?? string.Empty : string.Empty;
                                var artDesc = artEl.TryGetProperty("description", out var ad) ? ad.GetString() ?? string.Empty : string.Empty;

                                if (string.IsNullOrWhiteSpace(artName)) continue;

                                var existingArt = await _db.Artifacts
                                    .FirstOrDefaultAsync(a => a.TenantId == tenantId && a.Name == artName, ct);

                                if (existingArt is null)
                                {
                                    var artifact = new Artifact
                                    {
                                        Id = Guid.NewGuid(),
                                        TenantId = tenantId,
                                        Name = artName,
                                        Description = artDesc,
                                        IsShared = false,
                                        CreatedAt = DateTime.UtcNow,
                                        UpdatedAt = DateTime.UtcNow
                                    };
                                    _db.Artifacts.Add(artifact);

                                    var artifactDept = new ArtifactDepartment
                                    {
                                        ArtifactId = artifact.Id,
                                        DepartmentId = department.Id,
                                        CreatedAt = DateTime.UtcNow
                                    };
                                    _db.ArtifactDepartments.Add(artifactDept);
                                    createdArtifacts.Add(artifact);
                                }
                            }
                        }
                    }
                }

                var sharedArtifact = await UpsertSharedArtifactAsync(tenantId, root, ct);
                if (!createdArtifacts.Any(a => a.IsShared))
                    createdArtifacts.Add(sharedArtifact);
            }
        }
        catch
        {
            var sharedArtifact = await EnsureSharedArtifactAsync(tenantId, ct);
            if (!createdArtifacts.Any(a => a.IsShared))
                createdArtifacts.Add(sharedArtifact);
        }

        await _db.SaveChangesAsync(ct);

        return new SupervisorResult(
            Success: true,
            Output: $"Discovered {createdDepartments.Count} departments and {createdArtifacts.Count} artifacts.",
            RequiresUserConfirmation: false,
            ConfirmationMessage: null,
            SuggestedArtifactId: null,
            RoutedArtifactId: null,
            IngestedItems: [],
            AgentName: "Supervisor Agent",
            ExecutionId: executionId);
    }

    // ── Direct-handle: Query ─────────────────────────────────────────────────

    private async Task<SupervisorResult> HandleQueryAsync(SupervisorRequest request, Guid executionId, CancellationToken ct)
    {
        var tenantId = request.TenantId;
        var query = request.TextInput ?? string.Empty;

        var departments = await _db.Departments.AsNoTracking()
            .Where(d => d.TenantId == tenantId).ToListAsync(ct);

        var artifacts = await _db.Artifacts.AsNoTracking()
            .Include(a => a.ArtifactDepartments).ThenInclude(ad => ad.Department)
            .Where(a => a.TenantId == tenantId).ToListAsync(ct);

        var manifest = await _db.DepartmentManifests.AsNoTracking()
            .FirstOrDefaultAsync(dm => dm.TenantId == tenantId, ct);

        var tenantEntity = await _db.Tenants.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tenantId, ct);

        IReadOnlyList<BusinessContext> ragResults;
        try { ragResults = await SearchAllArtifactsAsync(query, tenantId, artifacts, topKPerArtifact: 3, ct); }
        catch { ragResults = []; }

        var providerConfig = await GetProviderConfigAsync(tenantId, ct);
        IChatClient? chatClient = providerConfig is not null ? _chatFactory.GetChatClient(providerConfig) : null;

        if (chatClient is null)
        {
            return new SupervisorResult(
                Success: false,
                Output: "No chat model configured. Set a ChatModelId via PUT /api/embedding-config/system (or tenant override) to enable analytical responses.",
                RequiresUserConfirmation: false,
                ConfirmationMessage: null,
                SuggestedArtifactId: null,
                RoutedArtifactId: null,
                IngestedItems: [],
                AgentName: "Supervisor Agent",
                ExecutionId: executionId);
        }

        // ── MCP gather loop ──────────────────────────────────────────────────
        var mcpResults = new List<(string IntegrationName, string ToolName, string Result)>();

        var integrationsWithTools = await _db.McpIntegrations.AsNoTracking()
            .Where(m => m.TenantId == tenantId && m.IsEnabled && m.CachedToolsJson != null)
            .ToListAsync(ct);

        if (integrationsWithTools.Count > 0)
        {
            const int maxGatherIterations = 3;
            for (int iter = 0; iter < maxGatherIterations; iter++)
            {
                var gatherCalls = await RunGatherIterationAsync(
                    query, ragResults, mcpResults, artifacts, integrationsWithTools, chatClient, ct);

                if (gatherCalls.Count == 0) break;

                var callTasks = gatherCalls.Select(async call =>
                {
                    var integration = integrationsWithTools
                        .FirstOrDefault(m => m.Name == call.IntegrationName);
                    if (integration is null) return;
                    try
                    {
                        var result = await _mcpClientService.CallToolAsync(
                            integration, call.ToolName, call.Arguments, ct);
                        lock (mcpResults)
                            mcpResults.Add((call.IntegrationName, call.ToolName, result));
                    }
                    catch { /* skip failed call */ }
                });

                await Task.WhenAll(callTasks);
            }
        }

        // Fire-and-forget: auto-ingest any externally gathered content
        if (mcpResults.Count > 0)
        {
            _ = Task.Run(async () =>
            {
                foreach (var (integrationName, toolName, result) in mcpResults.ToList())
                {
                    if (string.IsNullOrWhiteSpace(result)) continue;
                    try
                    {
                        await ProcessAsync(new SupervisorRequest(
                            TenantId: tenantId,
                            UserId: Guid.Empty,
                            UserRole: UserRole.Admin,
                            Capability: AgentCapability.Ingest,
                            TextInput: result,
                            FileBytes: null,
                            FileName: null,
                            MimeType: null,
                            HintArtifactId: null,
                            Source: integrationName,
                            Category: toolName), CancellationToken.None);
                    }
                    catch { /* swallow */ }
                }
            }, CancellationToken.None);
        }
        // ── end MCP gather loop ──────────────────────────────────────────────

        var orgName = tenantEntity?.Name ?? "the organisation";
        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var sb = new StringBuilder();

        sb.AppendLine("[ROLE]");
        sb.AppendLine($"You are a strategic business intelligence analyst for {orgName}.");
        sb.AppendLine("Your role is to synthesise organisational knowledge, assess feasibility, identify risks, and give actionable recommendations.");
        sb.AppendLine("You do NOT merely summarise stored data — you reason about it.");
        sb.AppendLine($"Today's date: {today}");
        sb.AppendLine();

        sb.AppendLine("[ORGANISATIONAL CONTEXT]");
        sb.AppendLine($"Organisation: {orgName}");

        if (departments.Count > 0)
        {
            sb.AppendLine("Departments:");
            foreach (var dept in departments)
            {
                sb.Append($"  - {dept.Name}");
                if (!string.IsNullOrWhiteSpace(dept.Description))
                    sb.Append($": {dept.Description}");
                sb.AppendLine();
            }
        }

        if (artifacts.Count > 0)
        {
            sb.AppendLine("Knowledge artifacts:");
            foreach (var art in artifacts)
            {
                var deptName = art.ArtifactDepartments.FirstOrDefault()?.Department?.Name;
                sb.Append($"  - {art.Name}");
                if (deptName is not null) sb.Append($" ({deptName})");
                if (!string.IsNullOrWhiteSpace(art.Description))
                    sb.Append($": {art.Description}");
                sb.AppendLine();
            }
        }

        if (!string.IsNullOrWhiteSpace(manifest?.Content))
        {
            sb.AppendLine("Strategic overview:");
            sb.AppendLine(manifest.Content);
        }

        sb.AppendLine();

        sb.AppendLine("[RETRIEVED EVIDENCE]");
        if (ragResults.Count > 0)
        {
            sb.AppendLine($"The following {ragResults.Count} knowledge chunks were retrieved as most relevant to this query:");
            for (int i = 0; i < ragResults.Count; i++)
            {
                var r = ragResults[i];
                sb.AppendLine($"--- chunk {i + 1} ---");
                sb.AppendLine(r.Text);
                var meta = new List<string>();
                if (!string.IsNullOrWhiteSpace(r.Source)) meta.Add($"source: {r.Source}");
                if (!string.IsNullOrWhiteSpace(r.Category)) meta.Add($"category: {r.Category}");
                if (meta.Count > 0) sb.AppendLine($"[{string.Join(", ", meta)}]");
            }
            sb.AppendLine("If you need information not covered here, say so explicitly.");
        }
        else
        {
            sb.AppendLine("No specific knowledge chunks were retrieved. Base your response on the organisational context above and be explicit about what is unknown.");
        }

        sb.AppendLine();

        if (mcpResults.Count > 0)
        {
            sb.AppendLine("[EXTERNAL CONTEXT]");
            sb.AppendLine("The following data was retrieved from live external integrations to supplement internal knowledge:");
            foreach (var (integName, toolName, result) in mcpResults)
            {
                sb.AppendLine($"--- {integName} / {toolName} ---");
                sb.AppendLine(result);
            }
            sb.AppendLine();
        }

        sb.AppendLine("[INSTRUCTIONS]");
        sb.AppendLine("Identify what kind of question this is, then respond in the most appropriate format:");
        sb.AppendLine();
        sb.AppendLine("FACTUAL LOOKUP (\"What is our SLA?\", \"What is our pricing?\")");
        sb.AppendLine("→ Direct answer, 1-3 sentences. Cite source/category if available. No headers.");
        sb.AppendLine();
        sb.AppendLine("STRATEGIC / FEASIBILITY (\"Can we expand to 3 districts?\", \"Is this approach viable?\")");
        sb.AppendLine("→ Structured analysis. Adapt section headings to the specific question — do not use generic labels.");
        sb.AppendLine("  Include: one-sentence verdict, evidence synthesis across all relevant dimensions, reasoned");
        sb.AppendLine("  assessment (facts vs inferences), top 3-5 specific risks, actionable steps, knowledge gaps.");
        sb.AppendLine();
        sb.AppendLine("COMPARISON (\"How do we compare to our competitor?\", \"What is the difference between X and Y?\")");
        sb.AppendLine("→ Side-by-side comparison. Lead with the sharpest distinction.");
        sb.AppendLine();
        sb.AppendLine("PROCESS / HOW-TO (\"How do we roll out to a new zone?\", \"What are the steps to do X?\")");
        sb.AppendLine("→ Numbered steps derived from the knowledge base.");
        sb.AppendLine();
        sb.AppendLine("STATUS / SUMMARY (\"What is the current state of X?\", \"What does our rollout plan look like?\")");
        sb.AppendLine("→ Brief current-state summary. Bullets for discrete facts. One paragraph max.");
        sb.AppendLine();
        sb.AppendLine("Do not force structure onto a simple question. Insight over volume.");

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, sb.ToString()),
            new(ChatRole.User, query)
        };

        try
        {
            var response = await chatClient.GetResponseAsync(messages, cancellationToken: ct);
            return new SupervisorResult(
                Success: true,
                Output: response.Text,
                RequiresUserConfirmation: false,
                ConfirmationMessage: null,
                SuggestedArtifactId: null,
                RoutedArtifactId: null,
                IngestedItems: [],
                AgentName: "Supervisor Agent",
                ExecutionId: executionId);
        }
        catch (Exception ex)
        {
            return new SupervisorResult(
                Success: false,
                Output: $"Query failed: {ex.Message}",
                RequiresUserConfirmation: false,
                ConfirmationMessage: null,
                SuggestedArtifactId: null,
                RoutedArtifactId: null,
                IngestedItems: [],
                AgentName: "Supervisor Agent",
                ExecutionId: executionId);
        }
    }

    // ── Private: context builder ─────────────────────────────────────────────

    private async Task<AgentContext> BuildContextAsync(
        IReadOnlyList<string> requiredContext,
        Guid tenantId,
        string? query,
        CancellationToken ct,
        int ragTopK = 5)
    {
        IReadOnlyList<Artifact> artifacts = [];
        IReadOnlyList<Department> departments = [];
        string? manifestContent = null;
        IReadOnlyList<BusinessContext> ragResults = [];

        foreach (var ctx in requiredContext)
        {
            switch (ctx)
            {
                case "artifacts":
                    artifacts = await _db.Artifacts
                        .Include(a => a.ArtifactDepartments).ThenInclude(ad => ad.Department)
                        .Where(a => a.TenantId == tenantId)
                        .ToListAsync(ct);
                    break;

                case "departments":
                    departments = await _db.Departments
                        .Where(d => d.TenantId == tenantId)
                        .ToListAsync(ct);
                    break;

                case "manifest":
                    var manifest = await _db.DepartmentManifests
                        .FirstOrDefaultAsync(dm => dm.TenantId == tenantId, ct);
                    manifestContent = manifest?.Content;
                    break;

                case "rag.results":
                    try
                    {
                        ragResults = artifacts.Count > 0
                            ? await SearchAllArtifactsAsync(query ?? string.Empty, tenantId, artifacts, topKPerArtifact: 3, ct)
                            : await _ragService.SearchAsync(query ?? string.Empty, tenantId, ragTopK, null, null, ct);
                    }
                    catch
                    {
                        ragResults = [];
                    }
                    break;
            }
        }

        return new AgentContext(artifacts, departments, manifestContent, ragResults);
    }

    // ── Private: per-artifact RAG sweep ─────────────────────────────────────

    private async Task<IReadOnlyList<BusinessContext>> SearchAllArtifactsAsync(
        string query,
        Guid tenantId,
        IReadOnlyList<Artifact> artifacts,
        int topKPerArtifact,
        CancellationToken ct)
    {
        var seen = new HashSet<Guid>();
        var merged = new List<BusinessContext>();

        foreach (var artifact in artifacts)
        {
            try
            {
                var results = await _ragService.SearchAsync(
                    query, tenantId, topKPerArtifact, artifact.Id, null, ct);
                foreach (var r in results)
                    if (seen.Add(r.Id)) merged.Add(r);
            }
            catch { /* skip failed artifact */ }
        }

        return merged;
    }

    // ── Private: MCP gather iteration ───────────────────────────────────────

    private record GatherCall(string IntegrationName, string ToolName, IDictionary<string, object?> Arguments);

    private async Task<IReadOnlyList<GatherCall>> RunGatherIterationAsync(
        string query,
        IReadOnlyList<BusinessContext> ragResults,
        IReadOnlyList<(string IntegrationName, string ToolName, string Result)> mcpResultsSoFar,
        IReadOnlyList<Artifact> artifacts,
        IReadOnlyList<McpIntegration> integrations,
        IChatClient chatClient,
        CancellationToken ct)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Query: \"{query}\"");
        sb.AppendLine();
        sb.AppendLine($"Internal evidence so far: {ragResults.Count} chunks retrieved across {artifacts.Count} knowledge areas.");

        if (mcpResultsSoFar.Count > 0)
        {
            sb.AppendLine("External data already fetched this session:");
            foreach (var (name, tool, _) in mcpResultsSoFar)
                sb.AppendLine($"  - {name} / {tool}");
        }

        sb.AppendLine();
        sb.AppendLine("Available external integrations:");
        foreach (var integ in integrations)
        {
            var tools = DeserializeCachedTools(integ.CachedToolsJson);
            var toolList = string.Join(", ", tools.Select(t =>
                string.IsNullOrWhiteSpace(t.Description) ? t.Name : $"{t.Name} — {t.Description}"));
            sb.AppendLine($"  - {integ.Name}: {toolList}");
        }

        sb.AppendLine();
        sb.AppendLine("If you have enough context to answer this question accurately, reply ONLY:");
        sb.AppendLine("  { \"ready\": true }");
        sb.AppendLine();
        sb.AppendLine("If external data would materially improve your answer, list ALL calls you need this round");
        sb.AppendLine("(they run in parallel, so request everything you need at once):");
        sb.AppendLine("  { \"ready\": false, \"calls\": [");
        sb.AppendLine("      { \"name\": \"IntegrationName\", \"tool\": \"toolName\", \"args\": { \"key\": \"value\" } }");
        sb.AppendLine("  ] }");
        sb.AppendLine();
        sb.AppendLine("Only request data that is genuinely needed. Do not repeat calls already made.");

        try
        {
            var response = await chatClient.GetResponseAsync(sb.ToString(), cancellationToken: ct);
            var text = response.Text?.Trim() ?? string.Empty;

            // Strip markdown code fences if present
            if (text.StartsWith("```")) text = text.Split('\n').Skip(1).TakeWhile(l => !l.StartsWith("```")).Aggregate((a, b) => a + "\n" + b);

            using var doc = JsonDocument.Parse(text);
            var root = doc.RootElement;

            if (root.TryGetProperty("ready", out var readyEl) && readyEl.GetBoolean())
                return [];

            if (!root.TryGetProperty("calls", out var callsEl) || callsEl.ValueKind != JsonValueKind.Array)
                return [];

            var result = new List<GatherCall>();
            foreach (var callEl in callsEl.EnumerateArray())
            {
                var name = callEl.TryGetProperty("name", out var n) ? n.GetString() ?? string.Empty : string.Empty;
                var tool = callEl.TryGetProperty("tool", out var t) ? t.GetString() ?? string.Empty : string.Empty;
                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(tool)) continue;

                var args = new Dictionary<string, object?>();
                if (callEl.TryGetProperty("args", out var argsEl) && argsEl.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in argsEl.EnumerateObject())
                        args[prop.Name] = prop.Value.ValueKind == JsonValueKind.String
                            ? (object?)prop.Value.GetString()
                            : prop.Value.Clone();
                }

                result.Add(new GatherCall(name, tool, args));
            }

            return result;
        }
        catch
        {
            return [];
        }
    }

    private static IReadOnlyList<McpToolInfo> DeserializeCachedTools(string? json)
    {
        if (string.IsNullOrEmpty(json)) return [];
        try
        {
            return JsonSerializer.Deserialize<List<McpToolInfo>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
        }
        catch { return []; }
    }

    // ── Private: ingest items ────────────────────────────────────────────────

    private async Task ExecuteIngestItemsAsync(
        IReadOnlyList<IngestItem> items,
        SupervisorRequest request,
        Guid? fallbackArtifactId,
        CancellationToken ct)
    {
        foreach (var item in items)
        {
            var artifactId = item.ArtifactId ?? fallbackArtifactId;
            await _ragService.IngestAsync(
                item.Text,
                request.TenantId,
                artifactId,
                item.Source ?? request.Source,
                item.Category ?? request.Category,
                ct);
        }
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    private async Task<EmbeddingProviderConfig?> GetProviderConfigAsync(Guid tenantId, CancellationToken ct)
    {
        var config = await _db.EmbeddingProviderConfigs
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.TenantId == tenantId, ct);

        if (config is null)
        {
            config = await _db.EmbeddingProviderConfigs
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.TenantId == null, ct);
        }

        return config;
    }

    private async Task<DepartmentManifest?> LoadManifestAsync(Guid tenantId, CancellationToken ct)
    {
        return await _db.DepartmentManifests
            .AsNoTracking()
            .FirstOrDefaultAsync(dm => dm.TenantId == tenantId, ct);
    }

    private async Task<(Department? dept, Artifact? artifact, Artifact? sharedArtifact)> CreateGeneralDepartmentAsync(
        Guid tenantId, CancellationToken ct)
    {
        Department? dept = null;
        Artifact? artifact = null;

        var existingDept = await _db.Departments
            .FirstOrDefaultAsync(d => d.TenantId == tenantId && d.Name == "General", ct);

        if (existingDept is null)
        {
            dept = new Department
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = "General",
                Description = "General knowledge and processes.",
                EstimatedSize = "small",
                CreatedAt = DateTime.UtcNow
            };
            _db.Departments.Add(dept);

            artifact = new Artifact
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = "General Knowledge",
                Description = "General organizational knowledge.",
                IsShared = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _db.Artifacts.Add(artifact);

            var generalArtifactDept = new ArtifactDepartment
            {
                ArtifactId = artifact.Id,
                DepartmentId = dept.Id,
                CreatedAt = DateTime.UtcNow
            };
            _db.ArtifactDepartments.Add(generalArtifactDept);
        }

        var sharedArtifact = await EnsureSharedArtifactAsync(tenantId, ct);

        return (dept, artifact, sharedArtifact);
    }

    private async Task<Artifact> UpsertSharedArtifactAsync(Guid tenantId, JsonElement root, CancellationToken ct)
    {
        string sharedName = "Company Knowledge";
        string sharedDesc = "Mission, values, strategy, cross-departmental policies.";

        if (root.TryGetProperty("sharedArtifact", out var sharedEl))
        {
            if (sharedEl.TryGetProperty("name", out var sn) && sn.GetString() is { } sName)
                sharedName = sName;
            if (sharedEl.TryGetProperty("description", out var sd) && sd.GetString() is { } sDesc)
                sharedDesc = sDesc;
        }

        var existing = await _db.Artifacts
            .FirstOrDefaultAsync(a => a.TenantId == tenantId && a.IsShared, ct);

        if (existing is not null)
        {
            existing.Name = sharedName;
            existing.Description = sharedDesc;
            existing.UpdatedAt = DateTime.UtcNow;
            return existing;
        }

        var newShared = new Artifact
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = sharedName,
            Description = sharedDesc,
            IsShared = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.Artifacts.Add(newShared);
        return newShared;
    }

    private async Task<Artifact> EnsureSharedArtifactAsync(Guid tenantId, CancellationToken ct)
    {
        var existing = await _db.Artifacts
            .FirstOrDefaultAsync(a => a.TenantId == tenantId && a.IsShared, ct);

        if (existing is not null)
            return existing;

        var newShared = new Artifact
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Company Knowledge",
            Description = "Mission, values, strategy, cross-departmental policies.",
            IsShared = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.Artifacts.Add(newShared);
        return newShared;
    }

    private static string BuildArtifactsJson(List<Artifact> artifacts)
    {
        var sb = new StringBuilder();
        sb.Append('[');
        for (int i = 0; i < artifacts.Count; i++)
        {
            var a = artifacts[i];
            sb.Append('{');
            sb.Append($"\"id\":\"{a.Id}\"");
            sb.Append($",\"name\":{JsonSerializer.Serialize(a.Name)}");
            sb.Append($",\"description\":{JsonSerializer.Serialize(a.Description)}");
            sb.Append($",\"isShared\":{(a.IsShared ? "true" : "false")}");
            sb.Append($",\"department\":{JsonSerializer.Serialize(a.ArtifactDepartments.FirstOrDefault()?.Department?.Name ?? string.Empty)}");
            sb.Append('}');
            if (i < artifacts.Count - 1) sb.Append(',');
        }
        sb.Append(']');
        return sb.ToString();
    }
}
