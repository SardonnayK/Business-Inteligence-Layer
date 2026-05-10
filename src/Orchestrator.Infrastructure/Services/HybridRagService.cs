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
    private readonly IEmbeddingProviderFactory _factory;

    /// <summary>
    /// Primary constructor used by production DI — resolves embedding config from the database per tenant.
    /// </summary>
    public HybridRagService(AppDbContext db, IEmbeddingProviderFactory factory)
    {
        _db = db;
        _factory = factory;
    }

    /// <summary>
    /// Test-friendly constructor — wraps a fixed <see cref="IEmbeddingGenerator{TInput,TEmbedding}"/>
    /// in a <see cref="FixedEmbeddingProviderFactory"/> so tests can inject a fake generator directly
    /// without needing a database-backed provider config.
    /// </summary>
    public HybridRagService(AppDbContext db, IEmbeddingGenerator<string, Embedding<float>> generator)
        : this(db, new FixedEmbeddingProviderFactory(generator))
    {
    }

    public async Task<BusinessContext> IngestAsync(
        string text, Guid tenantId, string? source = null, string? category = null, CancellationToken ct = default)
    {
        var generator = await GetEmbeddingGeneratorAsync(tenantId, ct);
        var result = await generator.GenerateAsync([text], cancellationToken: ct);
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
        var generator = await GetEmbeddingGeneratorAsync(tenantId, ct);
        var result = await generator.GenerateAsync([query], cancellationToken: ct);
        var queryVector = new Vector(result[0].Vector.ToArray());

        // Hybrid query: relational filter (TenantId) + vector ordering (L2 distance)
        return await _db.BusinessContexts
            .Where(bc => bc.TenantId == tenantId)
            .OrderBy(bc => bc.Embedding.L2Distance(queryVector))
            .Take(topK)
            .ToListAsync(ct);
    }

    private async Task<IEmbeddingGenerator<string, Embedding<float>>> GetEmbeddingGeneratorAsync(
        Guid tenantId, CancellationToken ct)
    {
        // When using the fixed factory (test path), there's no DB lookup needed.
        if (_factory is FixedEmbeddingProviderFactory fixedFactory)
            return fixedFactory.Generator;

        // 1. Try tenant-specific config
        var config = await _db.EmbeddingProviderConfigs
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.TenantId == tenantId, ct);

        // 2. Fall back to system default (TenantId IS NULL)
        if (config is null)
        {
            config = await _db.EmbeddingProviderConfigs
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.TenantId == null, ct);
        }

        // 3. Nothing configured at all
        if (config is null)
        {
            throw new InvalidOperationException(
                $"No embedding provider configured for tenant '{tenantId}' and no system default exists. " +
                "Configure one at PUT /api/embedding-config/system or PUT /api/embedding-config/tenant/{id}.");
        }

        return _factory.GetGenerator(config);
    }

    /// <summary>
    /// A trivial factory that always returns the same pre-built generator.
    /// Used by the test constructor to avoid needing a real DB-backed config.
    /// </summary>
    private sealed class FixedEmbeddingProviderFactory : IEmbeddingProviderFactory
    {
        internal IEmbeddingGenerator<string, Embedding<float>> Generator { get; }

        internal FixedEmbeddingProviderFactory(IEmbeddingGenerator<string, Embedding<float>> generator)
            => Generator = generator;

        public IEmbeddingGenerator<string, Embedding<float>> GetGenerator(EmbeddingProviderConfig config)
            => Generator;
    }
}
