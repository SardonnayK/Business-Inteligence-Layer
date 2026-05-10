using Microsoft.EntityFrameworkCore;
using Orchestrator.Core.Enums;
using Orchestrator.Core.Interfaces;
using Orchestrator.Infrastructure.Data;

namespace Orchestrator.Infrastructure.Services;

public class PermissionService : IPermissionService
{
    private readonly AppDbContext _db;

    public PermissionService(AppDbContext db) => _db = db;

    public async Task<bool> CanReadArtifactAsync(Guid userId, UserRole role, Guid artifactId, CancellationToken ct = default)
    {
        if (role == UserRole.Admin) return true;
        return await _db.ArtifactPermissions
            .AnyAsync(ap => ap.UserId == userId && ap.ArtifactId == artifactId && ap.CanRead, ct);
    }

    public async Task<bool> CanWriteArtifactAsync(Guid userId, UserRole role, Guid artifactId, CancellationToken ct = default)
    {
        if (role == UserRole.Admin) return true;
        return await _db.ArtifactPermissions
            .AnyAsync(ap => ap.UserId == userId && ap.ArtifactId == artifactId && ap.CanWrite, ct);
    }

    public async Task<IReadOnlyList<Guid>> GetReadableArtifactIdsAsync(Guid userId, UserRole role, Guid tenantId, CancellationToken ct = default)
    {
        if (role == UserRole.Admin)
        {
            return await _db.Artifacts
                .Where(a => a.TenantId == tenantId)
                .Select(a => a.Id)
                .ToListAsync(ct);
        }

        return await _db.ArtifactPermissions
            .Where(ap => ap.UserId == userId && ap.CanRead && ap.Artifact.TenantId == tenantId)
            .Select(ap => ap.ArtifactId)
            .ToListAsync(ct);
    }
}
