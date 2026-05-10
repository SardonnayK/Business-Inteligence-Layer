using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Orchestrator.Core.Entities;
using Orchestrator.Core.Interfaces;
using Orchestrator.Infrastructure.Data;

namespace Orchestrator.Infrastructure.Seeding;

public class DataSeeder
{
    private readonly AppDbContext _db;
    private readonly IHybridRagService _rag;
    private readonly ILogger<DataSeeder> _log;

    public DataSeeder(AppDbContext db, IHybridRagService rag, ILogger<DataSeeder> log)
    {
        _db = db;
        _rag = rag;
        _log = log;
    }

    public async Task<SeedResult> SeedAsync(CancellationToken ct = default)
    {
        var result = new SeedResult();

        await SeedCompanyAsync(
            SeedData.FibreCore.TenantId, "FibreCore Networks",
            SeedData.FibreCore.ProjectSeeds,
            SeedData.FibreCore.RequirementSeeds,
            SeedData.FibreCore.ContextChunks,
            result, ct);

        await SeedCompanyAsync(
            SeedData.SwiftFibre.TenantId, "SwiftFibre",
            SeedData.SwiftFibre.ProjectSeeds,
            SeedData.SwiftFibre.RequirementSeeds,
            SeedData.SwiftFibre.ContextChunks,
            result, ct);

        await SeedCompanyAsync(
            SystemSeedData.TenantId, SystemSeedData.TenantName,
            SystemSeedData.ProjectSeeds,
            SystemSeedData.RequirementSeeds,
            SystemSeedData.ContextChunks,
            result, ct);

        return result;
    }

    private async Task SeedCompanyAsync(
        Guid tenantId, string tenantName,
        SeedData.ProjectSeed[] projects,
        SeedData.RequirementSeed[] requirements,
        SeedData.ContextChunkSeed[] chunks,
        SeedResult result,
        CancellationToken ct)
    {
        // ── Tenant ────────────────────────────────────────────────────────────
        if (!await _db.Tenants.AnyAsync(t => t.Id == tenantId, ct))
        {
            _db.Tenants.Add(new Tenant { Id = tenantId, Name = tenantName, IsActive = true });
            await _db.SaveChangesAsync(ct);
            result.TenantsCreated++;
            _log.LogInformation("Created tenant {Name}", tenantName);
        }
        else
        {
            _log.LogInformation("Tenant {Name} already exists — skipping", tenantName);
        }

        // ── Projects ──────────────────────────────────────────────────────────
        foreach (var p in projects)
        {
            if (!await _db.Projects.AnyAsync(x => x.Id == p.Id, ct))
            {
                _db.Projects.Add(new Project
                {
                    Id = p.Id,
                    TenantId = p.TenantId,
                    Name = p.Name,
                    Description = p.Description
                });
                result.ProjectsCreated++;
            }
        }
        await _db.SaveChangesAsync(ct);

        // ── Requirements ──────────────────────────────────────────────────────
        foreach (var r in requirements)
        {
            if (!await _db.Requirements.AnyAsync(x => x.Id == r.Id, ct))
            {
                _db.Requirements.Add(new Requirement
                {
                    Id = r.Id,
                    ProjectId = r.ProjectId,
                    Content = r.Content,
                    Status = r.Status
                });
                result.RequirementsCreated++;
            }
        }
        await _db.SaveChangesAsync(ct);

        // ── Business Context (embeddings) ─────────────────────────────────────
        var existingCount = await _db.BusinessContexts.CountAsync(b => b.TenantId == tenantId, ct);
        if (existingCount >= chunks.Length)
        {
            _log.LogInformation("Business context for {Name} already seeded — skipping", tenantName);
            return;
        }

        _log.LogInformation("Ingesting {Count} context chunks for {Name}...", chunks.Length, tenantName);
        foreach (var chunk in chunks)
        {
            await _rag.IngestAsync(chunk.Text, tenantId, artifactId: null, chunk.Source, chunk.Category, ct);
            result.ContextChunksIngested++;
        }
        _log.LogInformation("Context ingestion complete for {Name}", tenantName);
    }
}

public record SeedResult
{
    public int TenantsCreated      { get; set; }
    public int ProjectsCreated     { get; set; }
    public int RequirementsCreated { get; set; }
    public int ContextChunksIngested { get; set; }
}
