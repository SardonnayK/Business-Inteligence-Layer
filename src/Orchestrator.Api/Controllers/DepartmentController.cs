using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Orchestrator.Core.Interfaces;
using Orchestrator.Infrastructure.Data;

namespace Orchestrator.Api.Controllers;

[ApiController]
[Route("api/departments")]
[Authorize]
public class DepartmentController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IArtifactSupervisorService _supervisorService;

    public DepartmentController(AppDbContext db, IArtifactSupervisorService supervisorService)
    {
        _db = db;
        _supervisorService = supervisorService;
    }

    private Guid GetTenantId() => Guid.Parse(User.FindFirstValue("tenant_id")!);

    /// <summary>List all departments for the tenant, with artifact count.</summary>
    [HttpGet]
    public async Task<IActionResult> GetDepartments(CancellationToken ct)
    {
        var tenantId = GetTenantId();
        var departments = await _db.Departments
            .AsNoTracking()
            .Include(d => d.ArtifactDepartments)
            .Where(d => d.TenantId == tenantId)
            .Select(d => new
            {
                d.Id,
                d.Name,
                d.Description,
                d.EstimatedSize,
                d.CreatedAt,
                artifactCount = d.ArtifactDepartments.Count
            })
            .ToListAsync(ct);

        return Ok(departments);
    }

    /// <summary>Run AI-assisted department and artifact discovery for a tenant. Admin only.</summary>
    [HttpPost("/api/tenants/{id:guid}/discover-departments")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DiscoverDepartments(Guid id, CancellationToken ct)
    {
        var result = await _supervisorService.DiscoverDepartmentsAsync(id, ct);

        return Ok(new
        {
            result.WasAiAssisted,
            departments = result.Departments.Select(d => new
            {
                d.Id,
                d.Name,
                d.Description,
                d.EstimatedSize,
                d.CreatedAt
            }),
            artifacts = result.Artifacts.Select(a => new
            {
                a.Id,
                a.Name,
                a.Description,
                a.IsShared,
                a.TenantId,
                a.CreatedAt,
                departments = a.ArtifactDepartments.Select(ad => new { id = ad.DepartmentId, name = ad.Department != null ? ad.Department.Name : null })
            })
        });
    }
}
