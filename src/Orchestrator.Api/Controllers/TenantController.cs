using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Orchestrator.Infrastructure.Data;

namespace Orchestrator.Api.Controllers;

[ApiController]
[Route("api/tenants")]
public class TenantController : ControllerBase
{
    private readonly AppDbContext _db;

    public TenantController(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>List all tenants.</summary>
    [HttpGet]
    public async Task<IActionResult> GetTenants(CancellationToken ct)
    {
        var tenants = await _db.Tenants
            .AsNoTracking()
            .OrderBy(t => t.Name)
            .Select(t => new { t.Id, t.Name, t.IsActive, t.CreatedAt })
            .ToListAsync(ct);

        return Ok(tenants);
    }

    /// <summary>List all projects for a given tenant.</summary>
    [HttpGet("{id:guid}/projects")]
    public async Task<IActionResult> GetProjects(Guid id, CancellationToken ct)
    {
        var tenantExists = await _db.Tenants.AnyAsync(t => t.Id == id, ct);
        if (!tenantExists)
            return NotFound();

        var projects = await _db.Projects
            .AsNoTracking()
            .Where(p => p.TenantId == id)
            .OrderBy(p => p.Name)
            .Select(p => new { p.Id, p.TenantId, p.Name, p.Description, p.CreatedAt })
            .ToListAsync(ct);

        return Ok(projects);
    }
}
