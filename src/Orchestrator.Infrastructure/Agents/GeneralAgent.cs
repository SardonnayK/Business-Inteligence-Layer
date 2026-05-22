using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Orchestrator.Core.Entities;
using Orchestrator.Core.Enums;
using Orchestrator.Core.Interfaces;
using Orchestrator.Infrastructure.Data;

namespace Orchestrator.Infrastructure.Agents;

public class GeneralAgent : IAgent
{
    private readonly IChatProviderFactory _chatFactory;
    private readonly IEmbeddingProviderFactory _embeddingFactory;
    private readonly AppDbContext _db;

    public GeneralAgent(
        IChatProviderFactory chatFactory,
        IEmbeddingProviderFactory embeddingFactory,
        AppDbContext db)
    {
        _chatFactory = chatFactory;
        _embeddingFactory = embeddingFactory;
        _db = db;
    }

    public AgentCapability Capability => AgentCapability.General;
    public IReadOnlyList<string> RequiredContext => ["artifacts", "departments", "manifest", "rag.results"];
    public string? InputSchema => null;

    public async Task<AgentResult> ExecuteAsync(AgentRequest request, CancellationToken ct)
    {
        var config = await GetProviderConfigAsync(request.TenantId, ct);
        IChatClient? chatClient = config is not null ? _chatFactory.GetChatClient(config) : null;

        if (chatClient is null)
        {
            return new AgentResult(
                Success: false,
                Output: "No chat provider configured for this tenant.",
                RoutedArtifactId: null,
                RequiresUserConfirmation: false,
                ConfirmationMessage: null,
                SuggestedArtifactId: null,
                IngestItems: []);
        }

        var systemPrompt = BuildSystemPrompt(request.Context);
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, systemPrompt),
            new(ChatRole.User, request.TextInput ?? string.Empty)
        };

        try
        {
            var response = await chatClient.GetResponseAsync(messages, cancellationToken: ct);
            return new AgentResult(
                Success: true,
                Output: response.Text,
                RoutedArtifactId: null,
                RequiresUserConfirmation: false,
                ConfirmationMessage: null,
                SuggestedArtifactId: null,
                IngestItems: []);
        }
        catch (Exception ex)
        {
            return new AgentResult(
                Success: false,
                Output: $"Chat request failed: {ex.Message}",
                RoutedArtifactId: null,
                RequiresUserConfirmation: false,
                ConfirmationMessage: null,
                SuggestedArtifactId: null,
                IngestItems: []);
        }
    }

    private static string BuildSystemPrompt(AgentContext context)
    {
        var sb = new StringBuilder();

        sb.AppendLine("[ROLE]");
        sb.AppendLine("You are a strategic business intelligence analyst with access to the organisation's full knowledge base.");
        sb.AppendLine("Synthesise, reason, and recommend — do not just quote what is stored.");
        sb.AppendLine();

        sb.AppendLine("[ORGANISATIONAL MAP]");

        if (context.Artifacts.Count > 0)
        {
            sb.AppendLine("Knowledge artifacts:");
            foreach (var art in context.Artifacts)
            {
                var deptName = art.ArtifactDepartments.FirstOrDefault()?.Department?.Name;
                sb.Append($"  - {art.Name}");
                if (deptName is not null) sb.Append($" ({deptName})");
                if (!string.IsNullOrWhiteSpace(art.Description))
                    sb.Append($": {art.Description}");
                sb.AppendLine();
            }
        }

        if (context.Departments.Count > 0)
        {
            sb.AppendLine("Departments:");
            foreach (var dept in context.Departments)
            {
                sb.Append($"  - {dept.Name}");
                if (!string.IsNullOrWhiteSpace(dept.Description))
                    sb.Append($": {dept.Description}");
                sb.AppendLine();
            }
        }

        if (!string.IsNullOrWhiteSpace(context.DepartmentManifest))
        {
            sb.AppendLine("Strategic overview:");
            sb.AppendLine(context.DepartmentManifest);
        }

        sb.AppendLine();

        sb.AppendLine("[RETRIEVED EVIDENCE]");
        if (context.RagResults.Count > 0)
        {
            for (int i = 0; i < context.RagResults.Count; i++)
            {
                var r = context.RagResults[i];
                sb.AppendLine($"--- chunk {i + 1} ---");
                sb.AppendLine(r.Text);
                var meta = new List<string>();
                if (!string.IsNullOrWhiteSpace(r.Source)) meta.Add($"source: {r.Source}");
                if (!string.IsNullOrWhiteSpace(r.Category)) meta.Add($"category: {r.Category}");
                if (meta.Count > 0) sb.AppendLine($"[{string.Join(", ", meta)}]");
            }
        }
        else
        {
            sb.AppendLine("No specific knowledge chunks were retrieved. Base your response on the organisational context above.");
        }

        sb.AppendLine();

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

        return sb.ToString();
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
}
