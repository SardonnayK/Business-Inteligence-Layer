using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Orchestrator.Core.Entities;
using Orchestrator.Infrastructure.Data;

namespace Orchestrator.Api.Controllers;

[ApiController]
[Route("api/tenants/{id:guid}/department-manifest")]
[Authorize]
public class DepartmentManifestController : ControllerBase
{
    private readonly AppDbContext _db;

    public DepartmentManifestController(AppDbContext db) => _db = db;

    private Guid GetTenantId() => Guid.Parse(User.FindFirstValue("tenant_id")!);
    private bool IsAdmin() => User.IsInRole("Admin");

    /// <summary>Get the department manifest for a tenant. Admins can read any tenant; Members can only read their own.</summary>
    [HttpGet]
    public async Task<IActionResult> GetManifest(Guid id, CancellationToken ct)
    {
        var callerTenantId = GetTenantId();

        if (!IsAdmin() && callerTenantId != id)
            return StatusCode(403, new { message = "You can only access your own tenant's manifest." });

        var manifest = await _db.DepartmentManifests
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.TenantId == id, ct);

        if (manifest is null)
            return NotFound(new { message = $"No department manifest found for tenant '{id}'." });

        return Ok(new
        {
            manifest.Id,
            manifest.TenantId,
            manifest.Content,
            manifest.UpdatedAt
        });
    }

    /// <summary>Upsert the department manifest and sync departments from it. Admin only.</summary>
    [HttpPut]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpsertManifest(Guid id, [FromBody] ManifestRequest request, CancellationToken ct)
    {
        var manifest = await _db.DepartmentManifests
            .FirstOrDefaultAsync(m => m.TenantId == id, ct);

        if (manifest is null)
        {
            manifest = new DepartmentManifest
            {
                Id = Guid.NewGuid(),
                TenantId = id,
                Content = request.Content,
                UpdatedAt = DateTime.UtcNow
            };
            _db.DepartmentManifests.Add(manifest);
        }
        else
        {
            manifest.Content = request.Content;
            manifest.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(ct);

        var syncedCount = await SyncDepartmentsFromManifestAsync(id, request.Content, ct);

        return Ok(new
        {
            manifest.Id,
            manifest.TenantId,
            manifest.Content,
            manifest.UpdatedAt,
            syncedDepartments = syncedCount
        });
    }

    /// <summary>Delete the department manifest for a tenant. Admin only.</summary>
    [HttpDelete]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteManifest(Guid id, CancellationToken ct)
    {
        var manifest = await _db.DepartmentManifests
            .FirstOrDefaultAsync(m => m.TenantId == id, ct);

        if (manifest is null)
            return NotFound(new { message = $"No department manifest found for tenant '{id}'." });

        _db.DepartmentManifests.Remove(manifest);
        await _db.SaveChangesAsync(ct);

        return NoContent();
    }

    /// <summary>Parse ## Heading lines from the manifest content and upsert missing Department rows for the tenant.</summary>
    private async Task<int> SyncDepartmentsFromManifestAsync(Guid tenantId, string content, CancellationToken ct)
    {
        var departmentNames = content
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => line.StartsWith("## "))
            .Select(line => line[3..].Trim())
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var existingNames = await _db.Departments
            .Where(d => d.TenantId == tenantId)
            .Select(d => d.Name)
            .ToListAsync(ct);

        var existingNamesSet = new HashSet<string>(existingNames, StringComparer.OrdinalIgnoreCase);

        int syncedCount = 0;
        foreach (var name in departmentNames.Where(n => !existingNamesSet.Contains(n)))
        {
            _db.Departments.Add(new Department
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = name,
                Description = string.Empty,
                EstimatedSize = "unknown",
                CreatedAt = DateTime.UtcNow
            });
            syncedCount++;
        }

        if (syncedCount > 0)
            await _db.SaveChangesAsync(ct);

        return syncedCount;
    }
}

public record ManifestRequest(string Content);
