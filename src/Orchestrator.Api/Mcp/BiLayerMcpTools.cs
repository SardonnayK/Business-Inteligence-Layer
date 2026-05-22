using System.ComponentModel;
using System.Text;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Server;
using Orchestrator.Core.Enums;
using Orchestrator.Core.Interfaces;
using Orchestrator.Infrastructure.Data;

namespace Orchestrator.Api.Mcp;

[McpServerToolType]
public class BiLayerMcpTools(ISupervisorAgent supervisor, AppDbContext db)
{
    [McpServerTool]
    [Description("Ingest text content into the BI knowledge base for a tenant. Use this to push emails, Slack messages, CRM notes, or any other text into the organisation's knowledge store.")]
    public async Task<string> Ingest(
        [Description("Tenant ID (GUID)")] string tenantId,
        [Description("The text content to ingest")] string text,
        [Description("Source system, e.g. gmail, slack, crm, manual")] string source,
        [Description("Content category, e.g. email, policy, meeting-notes, announcement")] string? category,
        CancellationToken ct)
    {
        if (!Guid.TryParse(tenantId, out var tid))
            return "Error: tenantId must be a valid GUID.";

        if (string.IsNullOrWhiteSpace(text))
            return "Error: text cannot be empty.";

        var result = await supervisor.ProcessAsync(new SupervisorRequest(
            TenantId: tid,
            UserId: Guid.Empty,
            UserRole: UserRole.Admin,
            Capability: AgentCapability.Ingest,
            TextInput: text,
            FileBytes: null,
            FileName: null,
            MimeType: null,
            HintArtifactId: null,
            Source: source,
            Category: category), ct);

        return result.Success
            ? $"Ingested successfully. Routed to artifact: {result.RoutedArtifactId?.ToString() ?? "unknown"}. Items ingested: {result.IngestedItems.Count}."
            : $"Ingest failed: {result.Output}";
    }

    [McpServerTool]
    [Description("Ask a strategic business intelligence question about a tenant's knowledge base. Returns a contextual analysis synthesised from the organisation's stored knowledge.")]
    public async Task<string> Query(
        [Description("Tenant ID (GUID)")] string tenantId,
        [Description("The business question to ask, e.g. 'Is expanding to 3 new districts feasible this quarter?'")] string question,
        CancellationToken ct)
    {
        if (!Guid.TryParse(tenantId, out var tid))
            return "Error: tenantId must be a valid GUID.";

        if (string.IsNullOrWhiteSpace(question))
            return "Error: question cannot be empty.";

        var result = await supervisor.ProcessAsync(new SupervisorRequest(
            TenantId: tid,
            UserId: Guid.Empty,
            UserRole: UserRole.Admin,
            Capability: AgentCapability.Query,
            TextInput: question,
            FileBytes: null,
            FileName: null,
            MimeType: null,
            HintArtifactId: null), ct);

        return result.Output ?? "No response generated.";
    }

    [McpServerTool]
    [Description("List all knowledge artifacts and departments for a tenant. Useful for understanding what knowledge domains are available before ingesting or querying.")]
    public async Task<string> ListKnowledge(
        [Description("Tenant ID (GUID)")] string tenantId,
        CancellationToken ct)
    {
        if (!Guid.TryParse(tenantId, out var tid))
            return "Error: tenantId must be a valid GUID.";

        var artifacts = await db.Artifacts.AsNoTracking()
            .Include(a => a.ArtifactDepartments).ThenInclude(ad => ad.Department)
            .Where(a => a.TenantId == tid)
            .ToListAsync(ct);

        var departments = await db.Departments.AsNoTracking()
            .Where(d => d.TenantId == tid)
            .ToListAsync(ct);

        var sb = new StringBuilder();

        sb.AppendLine("Departments:");
        if (departments.Count == 0)
            sb.AppendLine("  (none)");
        else
            foreach (var d in departments)
                sb.AppendLine($"  - {d.Name}: {d.Description}");

        sb.AppendLine();
        sb.AppendLine("Knowledge artifacts:");
        if (artifacts.Count == 0)
            sb.AppendLine("  (none)");
        else
            foreach (var a in artifacts)
            {
                var dept = a.ArtifactDepartments.FirstOrDefault()?.Department?.Name ?? "shared";
                sb.AppendLine($"  - {a.Name} ({dept}){(string.IsNullOrWhiteSpace(a.Description) ? "" : $": {a.Description}")}");
            }

        return sb.ToString();
    }
}
