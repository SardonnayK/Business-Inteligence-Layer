namespace Orchestrator.Core.Entities;

public class Artifact
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty; // semantic scope — used by supervisor for routing
    public bool IsShared { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public Tenant Tenant { get; set; } = null!;
    public ICollection<ArtifactDepartment> ArtifactDepartments { get; set; } = new List<ArtifactDepartment>();
    public ICollection<BusinessContext> BusinessContexts { get; set; } = new List<BusinessContext>();
    public ICollection<ArtifactPermission> Permissions { get; set; } = new List<ArtifactPermission>();
}
