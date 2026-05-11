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
            SeedData.FibreCore.DepartmentSeeds,
            SeedData.FibreCore.ArtifactSeeds,
            SeedData.FibreCore.Manifest,
            result, ct);

        await SeedCompanyAsync(
            SeedData.SwiftFibre.TenantId, "SwiftFibre",
            SeedData.SwiftFibre.ProjectSeeds,
            SeedData.SwiftFibre.RequirementSeeds,
            SeedData.SwiftFibre.ContextChunks,
            SeedData.SwiftFibre.DepartmentSeeds,
            SeedData.SwiftFibre.ArtifactSeeds,
            SeedData.SwiftFibre.Manifest,
            result, ct);

        await SeedCompanyAsync(
            SystemSeedData.TenantId, SystemSeedData.TenantName,
            SystemSeedData.ProjectSeeds,
            SystemSeedData.RequirementSeeds,
            SystemSeedData.ContextChunks,
            departments: null,
            SystemSeedData.ArtifactSeeds,
            departmentManifest: null,
            result, ct);

        return result;
    }

    private async Task SeedCompanyAsync(
        Guid tenantId, string tenantName,
        SeedData.ProjectSeed[] projects,
        SeedData.RequirementSeed[] requirements,
        SeedData.ContextChunkSeed[] chunks,
        SeedData.DepartmentSeed[]? departments,
        SeedData.ArtifactSeed[]? artifacts,
        string? departmentManifest,
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

        // ── Department Manifest ───────────────────────────────────────────────
        if (departmentManifest is not null &&
            !await _db.DepartmentManifests.AnyAsync(m => m.TenantId == tenantId, ct))
        {
            _db.DepartmentManifests.Add(new DepartmentManifest
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Content = departmentManifest,
                UpdatedAt = DateTime.UtcNow
            });
            await _db.SaveChangesAsync(ct);
            result.ManifestsCreated++;
            _log.LogInformation("Created department manifest for {Name}", tenantName);
        }

        // ── Departments ───────────────────────────────────────────────────────
        if (departments is not null)
        {
            foreach (var d in departments)
            {
                if (!await _db.Departments.AnyAsync(x => x.Id == d.Id, ct))
                {
                    _db.Departments.Add(new Department
                    {
                        Id = d.Id,
                        TenantId = d.TenantId,
                        Name = d.Name,
                        Description = d.Description,
                        EstimatedSize = d.EstimatedSize
                    });
                    result.DepartmentsCreated++;
                }
            }
            await _db.SaveChangesAsync(ct);
        }

        // ── Artifacts + Department links ──────────────────────────────────────
        if (artifacts is not null)
        {
            foreach (var a in artifacts)
            {
                if (!await _db.Artifacts.AnyAsync(x => x.Id == a.Id, ct))
                {
                    _db.Artifacts.Add(new Artifact
                    {
                        Id = a.Id,
                        TenantId = a.TenantId,
                        Name = a.Name,
                        Description = a.Description,
                        IsShared = a.IsShared
                    });
                    result.ArtifactsCreated++;
                }
            }
            await _db.SaveChangesAsync(ct);

            foreach (var a in artifacts.Where(x => x.DepartmentId.HasValue))
            {
                if (!await _db.ArtifactDepartments.AnyAsync(
                        ad => ad.ArtifactId == a.Id && ad.DepartmentId == a.DepartmentId!.Value, ct))
                {
                    _db.ArtifactDepartments.Add(new ArtifactDepartment
                    {
                        ArtifactId = a.Id,
                        DepartmentId = a.DepartmentId!.Value
                    });
                }
            }
            await _db.SaveChangesAsync(ct);
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
        }
        else
        {
            _log.LogInformation("Ingesting {Count} context chunks for {Name}...", chunks.Length, tenantName);
            foreach (var chunk in chunks)
            {
                await _rag.IngestAsync(chunk.Text, tenantId, chunk.ArtifactId, chunk.Source, chunk.Category, ct);
                result.ContextChunksIngested++;
            }
            _log.LogInformation("Context ingestion complete for {Name}", tenantName);
        }

        // ── Admin user ────────────────────────────────────────────────────────
        if (!await _db.TenantUsers.AnyAsync(u => u.TenantId == tenantId && u.Username == "admin", ct))
        {
            _db.TenantUsers.Add(new Core.Entities.TenantUser
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Username = "admin",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin123!", workFactor: 10),
                Role = Core.Enums.UserRole.Admin,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
            await _db.SaveChangesAsync(ct);
            _log.LogInformation("Created admin user for {Name}", tenantName);
        }
    }
}

public record SeedResult
{
    public int TenantsCreated         { get; set; }
    public int ManifestsCreated       { get; set; }
    public int DepartmentsCreated     { get; set; }
    public int ArtifactsCreated       { get; set; }
    public int ProjectsCreated        { get; set; }
    public int RequirementsCreated    { get; set; }
    public int ContextChunksIngested  { get; set; }
}
