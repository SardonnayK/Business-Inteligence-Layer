using Orchestrator.Core.Entities;

namespace Orchestrator.Core.Interfaces;

public interface IArtifactSupervisorService
{
    Task<Artifact> RouteToArtifactAsync(string text, Guid tenantId, CancellationToken ct = default);
    Task<DiscoveryResult> DiscoverDepartmentsAsync(Guid tenantId, CancellationToken ct = default);
}

public sealed class DiscoveryResult
{
    public IReadOnlyList<Department> Departments { get; init; } = [];
    public IReadOnlyList<Artifact> Artifacts { get; init; } = [];
    public bool WasAiAssisted { get; init; }
}
