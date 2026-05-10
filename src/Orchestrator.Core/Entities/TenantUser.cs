using Orchestrator.Core.Enums;

namespace Orchestrator.Core.Entities;

public class TenantUser
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.Member;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Tenant Tenant { get; set; } = null!;
    public ICollection<ArtifactPermission> ArtifactPermissions { get; set; } = new List<ArtifactPermission>();
}
