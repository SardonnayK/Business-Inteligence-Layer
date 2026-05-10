namespace Orchestrator.Core.Entities;

public class ArtifactPermission
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid ArtifactId { get; set; }
    public bool CanRead { get; set; } = true;
    public bool CanWrite { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public TenantUser User { get; set; } = null!;
    public Artifact Artifact { get; set; } = null!;
}
