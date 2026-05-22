namespace Orchestrator.Core.Entities;

public class McpIntegration
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ServerUrl { get; set; } = string.Empty;
    public string? ApiKey { get; set; }
    public string AuthHeaderName { get; set; } = "Authorization";
    public string ToolNamesJson { get; set; } = "[]";
    public string? CachedToolsJson { get; set; }
    public bool IsEnabled { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public Tenant Tenant { get; set; } = null!;
}
