using Orchestrator.Core.Enums;

namespace Orchestrator.Core.Interfaces;

public interface IPermissionService
{
    Task<bool> CanReadArtifactAsync(Guid userId, UserRole role, Guid artifactId, CancellationToken ct = default);
    Task<bool> CanWriteArtifactAsync(Guid userId, UserRole role, Guid artifactId, CancellationToken ct = default);
    Task<IReadOnlyList<Guid>> GetReadableArtifactIdsAsync(Guid userId, UserRole role, Guid tenantId, CancellationToken ct = default);
}
