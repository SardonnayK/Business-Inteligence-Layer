namespace Orchestrator.Core.Entities;

public class Department
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string EstimatedSize { get; set; } = "unknown"; // "small" | "medium" | "large"
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Tenant Tenant { get; set; } = null!;
    public ICollection<ArtifactDepartment> ArtifactDepartments { get; set; } = new List<ArtifactDepartment>();
}
