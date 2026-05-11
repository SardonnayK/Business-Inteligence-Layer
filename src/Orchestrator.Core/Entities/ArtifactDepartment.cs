namespace Orchestrator.Core.Entities;

public class ArtifactDepartment
{
    public Guid ArtifactId { get; set; }
    public Guid DepartmentId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Artifact Artifact { get; set; } = null!;
    public Department Department { get; set; } = null!;
}
