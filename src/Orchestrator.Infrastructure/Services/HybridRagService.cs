using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Orchestrator.Core.Entities;
using Orchestrator.Core.Interfaces;
using Orchestrator.Infrastructure.Data;
using Pgvector;
using Pgvector.EntityFrameworkCore;

namespace Orchestrator.Infrastructure.Services;

public class HybridRagService : IHybridRagService
{
    private readonly AppDbContext _db;
    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddings;

    public HybridRagService(AppDbContext db, IEmbeddingGenerator<string, Embedding<float>> embeddings)
    {
        _db = db;
        _embeddings = embeddings;
    }

    public async Task<BusinessContext> IngestAsync(
        string text, Guid tenantId, string? source = null, string? category = null, CancellationToken ct = default)
    {
        var result = await _embeddings.GenerateAsync([text], cancellationToken: ct);
        var context = new BusinessContext
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Text = text,
            Embedding = new Vector(result[0].Vector.ToArray()),
            Source = source,
            Category = category,
            CreatedAt = DateTime.UtcNow
        };

        _db.BusinessContexts.Add(context);
        await _db.SaveChangesAsync(ct);
        return context;
    }

    public async Task<IReadOnlyList<BusinessContext>> SearchAsync(
        string query, Guid tenantId, int topK = 5, CancellationToken ct = default)
    {
        var result = await _embeddings.GenerateAsync([query], cancellationToken: ct);
        var queryVector = new Vector(result[0].Vector.ToArray());

        // Hybrid query: relational filter (TenantId) + vector ordering (L2 distance)
        return await _db.BusinessContexts
            .Where(bc => bc.TenantId == tenantId)
            .OrderBy(bc => bc.Embedding.L2Distance(queryVector))
            .Take(topK)
            .ToListAsync(ct);
    }
}
