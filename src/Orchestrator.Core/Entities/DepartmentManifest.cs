namespace Orchestrator.Core.Entities;

public class DepartmentManifest
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }

    /// <summary>
    /// Free-text description of the tenant's department structure and sizing rules.
    /// Injected into AI prompts to guide department discovery and artifact routing.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Tenant Tenant { get; set; } = null!;
}
