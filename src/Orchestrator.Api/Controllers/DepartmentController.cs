using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Orchestrator.Core.Interfaces;
using Orchestrator.Infrastructure.Data;

namespace Orchestrator.Api.Controllers;

[ApiController]
[Route("api/departments")]
public class DepartmentController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IArtifactSupervisorService _supervisorService;

    public DepartmentController(AppDbContext db, IArtifactSupervisorService supervisorService)
    {
        _db = db;
        _supervisorService = supervisorService;
    }

    /// <summary>List all departments for the tenant, with artifact count.</summary>
    [HttpGet]
    public async Task<IActionResult> GetDepartments(
        [FromHeader(Name = "X-Tenant-Id")] Guid tenantId,
        CancellationToken ct)
    {
        var departments = await _db.Departments
            .AsNoTracking()
            .Where(d => d.TenantId == tenantId)
            .Select(d => new
            {
                d.Id,
                d.Name,
                d.Description,
                d.EstimatedSize,
                d.CreatedAt,
                artifactCount = _db.Artifacts.Count(a => a.DepartmentId == d.Id)
            })
            .ToListAsync(ct);

        return Ok(departments);
    }

    /// <summary>Run AI-assisted department and artifact discovery for a tenant.</summary>
    [HttpPost("/api/tenants/{id:guid}/discover-departments")]
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
                a.DepartmentId,
                a.TenantId,
                a.CreatedAt
            })
        });
    }
}
