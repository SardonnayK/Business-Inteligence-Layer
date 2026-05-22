using Microsoft.EntityFrameworkCore;
using Orchestrator.Core.Entities;
using Orchestrator.Core.Enums;
using Orchestrator.Core.Interfaces;
using Orchestrator.Infrastructure.Data;

namespace Orchestrator.Infrastructure.Services;

public class ArtifactSupervisorService : IArtifactSupervisorService
{
    private readonly ISupervisorAgent _supervisor;
    private readonly AppDbContext _db;

    public ArtifactSupervisorService(ISupervisorAgent supervisor, AppDbContext db)
    {
        _supervisor = supervisor;
        _db = db;
    }

    public async Task<Artifact> RouteToArtifactAsync(string text, Guid tenantId, CancellationToken ct = default)
    {
        var result = await _supervisor.ProcessAsync(new SupervisorRequest(
            TenantId: tenantId,
            UserId: Guid.Empty,
            UserRole: UserRole.Admin,
            Capability: AgentCapability.Ingest,
            TextInput: text,
            FileBytes: null,
            FileName: null,
            MimeType: null,
            HintArtifactId: null,
            SkipAutoIngest: true), ct);

        if (result.RoutedArtifactId.HasValue)
        {
            var artifact = await _db.Artifacts
                .Include(a => a.ArtifactDepartments).ThenInclude(ad => ad.Department)
                .FirstOrDefaultAsync(a => a.Id == result.RoutedArtifactId.Value, ct);
            if (artifact is not null) return artifact;
        }

        // Fallback: return or create a shared artifact
        var shared = await _db.Artifacts.FirstOrDefaultAsync(a => a.TenantId == tenantId && a.IsShared, ct);
        if (shared is not null) return shared;

        shared = new Artifact
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Company Knowledge",
            IsShared = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.Artifacts.Add(shared);
        await _db.SaveChangesAsync(ct);
        return shared;
    }

    public async Task<DiscoveryResult> DiscoverDepartmentsAsync(Guid tenantId, CancellationToken ct = default)
    {
        await _supervisor.ProcessAsync(new SupervisorRequest(
            TenantId: tenantId,
            UserId: Guid.Empty,
            UserRole: UserRole.Admin,
            Capability: AgentCapability.Discover,
            TextInput: null,
            FileBytes: null,
            FileName: null,
            MimeType: null,
            HintArtifactId: null), ct);

        var departments = await _db.Departments.Where(d => d.TenantId == tenantId).ToListAsync(ct);
        var artifacts = await _db.Artifacts.Where(a => a.TenantId == tenantId).ToListAsync(ct);

        return new DiscoveryResult
        {
            Departments = departments,
            Artifacts = artifacts,
            WasAiAssisted = true
        };
    }
}
