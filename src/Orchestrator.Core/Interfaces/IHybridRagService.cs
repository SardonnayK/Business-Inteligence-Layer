using Orchestrator.Core.Entities;

namespace Orchestrator.Core.Interfaces;

public interface IHybridRagService
{
    Task<BusinessContext> IngestAsync(string text, Guid tenantId, string? source = null, string? category = null, CancellationToken ct = default);
    Task<IReadOnlyList<BusinessContext>> SearchAsync(string query, Guid tenantId, int topK = 5, CancellationToken ct = default);
}
