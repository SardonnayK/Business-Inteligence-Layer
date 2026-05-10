using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Orchestrator.Core.Enums;
using Orchestrator.Core.Interfaces;
using Orchestrator.Infrastructure.Data;

namespace Orchestrator.Api.Controllers;

[ApiController]
[Route("api/artifacts")]
[Authorize]
public class ArtifactController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IPermissionService _permissions;

    public ArtifactController(AppDbContext db, IPermissionService permissions)
    {
        _db = db;
        _permissions = permissions;
    }

    private Guid GetTenantId() => Guid.Parse(User.FindFirstValue("tenant_id")!);
    private Guid GetUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private UserRole GetRole() => Enum.Parse<UserRole>(User.FindFirstValue(ClaimTypes.Role)!);

    /// <summary>List artifacts visible to the caller. Admins see all; Members see only their permitted artifacts.</summary>
    [HttpGet]
    public async Task<IActionResult> GetArtifacts(CancellationToken ct)
    {
        var tenantId = GetTenantId();
        var userId = GetUserId();
        var role = GetRole();

        IReadOnlyList<Guid>? readableIds = null;
        if (role != UserRole.Admin)
            readableIds = await _permissions.GetReadableArtifactIdsAsync(userId, role, tenantId, ct);

        var query = _db.Artifacts
            .AsNoTracking()
            .Include(a => a.Department)
            .Where(a => a.TenantId == tenantId);

        if (readableIds is not null)
            query = query.Where(a => readableIds.Contains(a.Id));

        var artifacts = await query
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
    public async Task<IActionResult> GetArtifactContexts(Guid id, CancellationToken ct)
    {
        var tenantId = GetTenantId();
        var artifact = await _db.Artifacts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id && a.TenantId == tenantId, ct);

        if (artifact is null)
            return NotFound(new { message = $"Artifact '{id}' not found for this tenant." });

        var role = GetRole();
        if (role != UserRole.Admin)
        {
            var canRead = await _permissions.CanReadArtifactAsync(GetUserId(), role, id, ct);
            if (!canRead)
                return StatusCode(403, new { message = "You do not have read permission on this artifact." });
        }

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

    /// <summary>Delete all BusinessContexts for an artifact (re-ingestion prep). Admin only.</summary>
    [HttpDelete("{id:guid}/contexts")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteArtifactContexts(Guid id, CancellationToken ct)
    {
        var tenantId = GetTenantId();
        var artifact = await _db.Artifacts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id && a.TenantId == tenantId, ct);

        if (artifact is null)
            return NotFound(new { message = $"Artifact '{id}' not found for this tenant." });

        var contexts = await _db.BusinessContexts.Where(bc => bc.ArtifactId == id).ToListAsync(ct);
        var deletedCount = contexts.Count;
        _db.BusinessContexts.RemoveRange(contexts);
        await _db.SaveChangesAsync(ct);

        return Ok(new { deletedCount });
    }
}
