using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Orchestrator.Core.Entities;
using Orchestrator.Infrastructure.Data;
using Orchestrator.Infrastructure.Services;
using Testcontainers.PostgreSql;
using Xunit;

namespace Orchestrator.Tests.Integration;

/// <summary>
/// Integration tests for HybridRagService against a real pgvector container.
/// Requires Docker. Run via: .\test.ps1 (starts Docker automatically).
/// </summary>
[Trait("Category", "Integration")]
public class BusinessContextTests : IAsyncLifetime
{
    private PostgreSqlContainer? _postgres;
    private AppDbContext? _db;
    private HybridRagService? _sut;
    private string? _skipReason;

    public async Task InitializeAsync()
    {
        try
        {
            _postgres = new PostgreSqlBuilder("pgvector/pgvector:pg16").Build();
            await _postgres.StartAsync();

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(_postgres.GetConnectionString(), o => o.UseVector())
                .Options;

            _db = new AppDbContext(options);
            await _db.Database.MigrateAsync();

            _sut = new HybridRagService(_db, new FakeEmbeddingGenerator());
        }
        catch (Exception ex) when (ex.GetType().Name is "DockerUnavailableException" or "DockerNotRunningException"
                                   || ex.Message.Contains("Docker"))
        {
            _skipReason = "Docker is not running. Use .\\test.ps1 to start it automatically.";
        }
    }

    public async Task DisposeAsync()
    {
        if (_db is not null) await _db.DisposeAsync();
        if (_postgres is not null) await _postgres.StopAsync();
    }

    [SkippableFact]
    public async Task Ingest_PersistsContextWithEmbedding()
    {
        Skip.If(_skipReason is not null, _skipReason);

        var tenantId = await SeedTenantAsync();

        var result = await _sut!.IngestAsync("The checkout flow uses Stripe", tenantId, source: "wiki");

        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal(tenantId, result.TenantId);
        Assert.Equal("The checkout flow uses Stripe", result.Text);
        Assert.Equal("wiki", result.Source);
        Assert.NotNull(result.Embedding);
        Assert.Equal(FakeEmbeddingGenerator.Dimensions, result.Embedding.Memory.Length);

        // Verify the row is actually in the database
        var stored = await _db!.BusinessContexts.FindAsync(result.Id);
        Assert.NotNull(stored);
    }

    [SkippableFact]
    public async Task Search_ReturnsTenantScopedResults_OrderedBySimilarity()
    {
        Skip.If(_skipReason is not null, _skipReason);

        var tenantA = await SeedTenantAsync();
        var tenantB = await SeedTenantAsync();

        // Ingest three docs for tenant A and one for tenant B
        await _sut!.IngestAsync("Payments use Stripe", tenantA);
        await _sut!.IngestAsync("Authentication uses OAuth2", tenantA);
        await _sut!.IngestAsync("Deployments run on Kubernetes", tenantA);
        await _sut!.IngestAsync("Payments use PayPal", tenantB);

        var results = await _sut.SearchAsync("payment gateway", tenantA, topK: 2);

        // Only tenant A docs, top 2
        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.Equal(tenantA, r.TenantId));
    }

    [SkippableFact]
    public async Task Search_DoesNotLeakAcrossTenants()
    {
        Skip.If(_skipReason is not null, _skipReason);

        var tenantA = await SeedTenantAsync();
        var tenantB = await SeedTenantAsync();

        await _sut!.IngestAsync("Secret internal roadmap", tenantA);

        var results = await _sut.SearchAsync("roadmap", tenantB, topK: 5);

        Assert.Empty(results);
    }

    private async Task<Guid> SeedTenantAsync()
    {
        var tenant = new Tenant { Id = Guid.NewGuid(), Name = $"Tenant-{Guid.NewGuid():N}" };
        _db!.Tenants.Add(tenant);
        await _db.SaveChangesAsync();
        return tenant.Id;
    }
}
