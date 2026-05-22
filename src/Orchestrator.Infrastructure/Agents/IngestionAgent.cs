using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Orchestrator.Core.Entities;
using Orchestrator.Core.Enums;
using Orchestrator.Core.Interfaces;
using Orchestrator.Infrastructure.Data;
using Orchestrator.Infrastructure.Services;

namespace Orchestrator.Infrastructure.Agents;

public class IngestionAgent : IAgent
{
    private readonly IChatProviderFactory _chatFactory;
    private readonly IEmbeddingProviderFactory _embeddingFactory;
    private readonly AppDbContext _db;
    private readonly ITextExtractor _textExtractor;

    public IngestionAgent(
        IChatProviderFactory chatFactory,
        IEmbeddingProviderFactory embeddingFactory,
        AppDbContext db,
        ITextExtractor textExtractor)
    {
        _chatFactory = chatFactory;
        _embeddingFactory = embeddingFactory;
        _db = db;
        _textExtractor = textExtractor;
    }

    public AgentCapability Capability => AgentCapability.Ingest;
    public IReadOnlyList<string> RequiredContext => ["artifacts", "manifest"];
    public string? InputSchema => null;

    public async Task<AgentResult> ExecuteAsync(AgentRequest request, CancellationToken ct)
    {
        // Extract text from file bytes or fall back to TextInput
        string text;
        if (request.FileBytes is { Length: > 0 })
        {
            text = _textExtractor.Extract(request.FileBytes, request.MimeType, request.FileName);
        }
        else
        {
            text = request.TextInput ?? string.Empty;
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return new AgentResult(
                Success: false,
                Output: "No text content to ingest.",
                RoutedArtifactId: null,
                RequiresUserConfirmation: false,
                ConfirmationMessage: null,
                SuggestedArtifactId: null,
                IngestItems: []);
        }

        var artifacts = request.Context.Artifacts.ToList();

        // Bootstrap shared artifact if none exist
        if (artifacts.Count == 0)
        {
            var bootstrapped = await BootstrapAsync(request.TenantId, ct);
            return BuildResult(text, bootstrapped, request.HintArtifactId);
        }

        var config = await GetProviderConfigAsync(request.TenantId, ct);
        IChatClient? chatClient = config is not null ? _chatFactory.GetChatClient(config) : null;

        if (chatClient is not null)
        {
            var routed = await AiRouteAsync(text, artifacts, chatClient, request.Context.DepartmentManifest, request.TenantId, ct);
            return BuildResult(text, routed, request.HintArtifactId);
        }

        // No chat client — fall back to shared artifact
        var fallback = artifacts.FirstOrDefault(a => a.IsShared) ?? artifacts.First();
        return BuildResult(text, fallback, request.HintArtifactId);
    }

    private static AgentResult BuildResult(string text, Artifact artifact, Guid? hintArtifactId)
    {
        // If AI routed to a different artifact than the hint, signal a reroute via SuggestedArtifactId
        Guid? suggested = (hintArtifactId.HasValue && hintArtifactId.Value != artifact.Id)
            ? artifact.Id
            : null;

        return new AgentResult(
            Success: true,
            Output: null,
            RoutedArtifactId: artifact.Id,
            RequiresUserConfirmation: false,
            ConfirmationMessage: null,
            SuggestedArtifactId: suggested,
            IngestItems: [new IngestItem(text, null, null, artifact.Id)]);
    }

    private async Task<Artifact> AiRouteAsync(
        string text,
        List<Artifact> artifacts,
        IChatClient chatClient,
        string? manifest,
        Guid tenantId,
        CancellationToken ct)
    {
        try
        {
            var artifactsJson = BuildArtifactsJson(artifacts);

            var prompt =
                "You are a knowledge routing supervisor. Route the following text to the correct artifact.\n\n" +
                (manifest is not null ? "Department manifest for this tenant:\n" + manifest + "\n\n" : "") +
                "Artifacts (JSON):\n" + artifactsJson + "\n\n" +
                "Text to route:\n\"" + text + "\"\n\n" +
                "Reply with ONLY valid JSON — pick one format:\n" +
                "{\"action\":\"route\",\"artifactId\":\"<uuid>\"}\n" +
                "{\"action\":\"create\",\"departmentName\":\"<name>\",\"artifactName\":\"<name>\",\"artifactDescription\":\"<one sentence>\"}\n" +
                "{\"action\":\"shared\"}";

            var response = await chatClient.GetResponseAsync(prompt, cancellationToken: ct);
            return await ParseAndApplyRouteResponseAsync(response.Text, artifacts, tenantId, ct);
        }
        catch
        {
            return artifacts.FirstOrDefault(a => a.IsShared) ?? artifacts.First();
        }
    }

    private async Task<Artifact> ParseAndApplyRouteResponseAsync(
        string? responseText,
        List<Artifact> artifacts,
        Guid tenantId,
        CancellationToken ct)
    {
        var sharedArtifact = artifacts.FirstOrDefault(a => a.IsShared) ?? artifacts.First();

        if (string.IsNullOrWhiteSpace(responseText))
            return sharedArtifact;

        try
        {
            using var doc = JsonDocument.Parse(responseText.Trim());
            var root = doc.RootElement;

            if (!root.TryGetProperty("action", out var actionProp))
                return sharedArtifact;

            switch (actionProp.GetString())
            {
                case "route":
                {
                    if (!root.TryGetProperty("artifactId", out var idProp))
                        return sharedArtifact;

                    if (!Guid.TryParse(idProp.GetString(), out var artifactId))
                        return sharedArtifact;

                    return artifacts.FirstOrDefault(a => a.Id == artifactId) ?? sharedArtifact;
                }

                case "create":
                {
                    var departmentName = root.TryGetProperty("departmentName", out var dn)
                        ? dn.GetString() ?? "New Department"
                        : "New Department";
                    var artifactName = root.TryGetProperty("artifactName", out var an)
                        ? an.GetString() ?? "New Artifact"
                        : "New Artifact";
                    var artifactDesc = root.TryGetProperty("artifactDescription", out var ad)
                        ? ad.GetString() ?? string.Empty
                        : string.Empty;

                    var department = await _db.Departments
                        .FirstOrDefaultAsync(d => d.TenantId == tenantId && d.Name == departmentName, ct);

                    if (department is null)
                    {
                        department = new Department
                        {
                            Id = Guid.NewGuid(),
                            TenantId = tenantId,
                            Name = departmentName,
                            Description = $"Department for {departmentName}.",
                            EstimatedSize = "small",
                            CreatedAt = DateTime.UtcNow
                        };
                        _db.Departments.Add(department);
                    }

                    var newArtifact = new Artifact
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenantId,
                        Name = artifactName,
                        Description = artifactDesc,
                        IsShared = false,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    _db.Artifacts.Add(newArtifact);

                    var artifactDepartment = new ArtifactDepartment
                    {
                        ArtifactId = newArtifact.Id,
                        DepartmentId = department.Id,
                        CreatedAt = DateTime.UtcNow
                    };
                    _db.ArtifactDepartments.Add(artifactDepartment);
                    await _db.SaveChangesAsync(ct);

                    artifactDepartment.Department = department;
                    newArtifact.ArtifactDepartments.Add(artifactDepartment);
                    return newArtifact;
                }

                case "shared":
                    return sharedArtifact;

                default:
                    return sharedArtifact;
            }
        }
        catch
        {
            return sharedArtifact;
        }
    }

    private async Task<Artifact> BootstrapAsync(Guid tenantId, CancellationToken ct)
    {
        var sharedArtifact = new Artifact
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Company Knowledge",
            Description = "General company knowledge, mission, values, strategy, and cross-departmental policies.",
            IsShared = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.Artifacts.Add(sharedArtifact);
        await _db.SaveChangesAsync(ct);
        return sharedArtifact;
    }

    private async Task<EmbeddingProviderConfig?> GetProviderConfigAsync(Guid tenantId, CancellationToken ct)
    {
        var config = await _db.EmbeddingProviderConfigs.AsNoTracking()
            .FirstOrDefaultAsync(e => e.TenantId == tenantId, ct);
        if (config is null)
            config = await _db.EmbeddingProviderConfigs.AsNoTracking()
                .FirstOrDefaultAsync(e => e.TenantId == null, ct);
        return config;
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
