using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Orchestrator.Infrastructure.Data;

namespace Orchestrator.Api.Controllers;

[ApiController]
[Route("api/artifacts")]
public class ArtifactController : ControllerBase
{
    private readonly AppDbContext _db;

    public ArtifactController(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>List all artifacts for the tenant, with chunk count and department info.</summary>
    [HttpGet]
    public async Task<IActionResult> GetArtifacts(
        [FromHeader(Name = "X-Tenant-Id")] Guid tenantId,
        CancellationToken ct)
    {
        var artifacts = await _db.Artifacts
            .AsNoTracking()
            .Include(a => a.Department)
            .Where(a => a.TenantId == tenantId)
            .Select(a => new
            {
                a.Id,
                a.Name,
                a.Description,
                a.IsShared,
                a.DepartmentId,
                departmentName = a.Department != null ? a.Department.Name : null,
                a.CreatedAt,
                a.UpdatedAt,
                chunkCount = _db.BusinessContexts.Count(bc => bc.ArtifactId == a.Id)
            })
            .ToListAsync(ct);

        return Ok(artifacts);
    }

    /// <summary>List all BusinessContexts (chunks) for a specific artifact, without the embedding vector.</summary>
    [HttpGet("{id:guid}/contexts")]
    public async Task<IActionResult> GetArtifactContexts(
        [FromHeader(Name = "X-Tenant-Id")] Guid tenantId,
        Guid id,
        CancellationToken ct)
    {
        // Verify the artifact belongs to the tenant
        var artifact = await _db.Artifacts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id && a.TenantId == tenantId, ct);

        if (artifact is null)
            return NotFound(new { message = $"Artifact '{id}' not found for this tenant." });

        var contexts = await _db.BusinessContexts
            .AsNoTracking()
            .Where(bc => bc.ArtifactId == id)
            .Select(bc => new
            {
                bc.Id,
                bc.Text,
                bc.Source,
                bc.Category,
                bc.ArtifactId,
                bc.TenantId,
                bc.CreatedAt
            })
            .ToListAsync(ct);

        return Ok(contexts);
    }

    /// <summary>Delete all BusinessContexts for an artifact (re-ingestion prep).</summary>
    [HttpDelete("{id:guid}/contexts")]
    public async Task<IActionResult> DeleteArtifactContexts(
        [FromHeader(Name = "X-Tenant-Id")] Guid tenantId,
        Guid id,
        CancellationToken ct)
    {
        // Verify the artifact belongs to the tenant
        var artifact = await _db.Artifacts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id && a.TenantId == tenantId, ct);

        if (artifact is null)
            return NotFound(new { message = $"Artifact '{id}' not found for this tenant." });

        var contexts = await _db.BusinessContexts
            .Where(bc => bc.ArtifactId == id)
            .ToListAsync(ct);

        var deletedCount = contexts.Count;
        _db.BusinessContexts.RemoveRange(contexts);
        await _db.SaveChangesAsync(ct);

        return Ok(new { deletedCount });
    }
}
