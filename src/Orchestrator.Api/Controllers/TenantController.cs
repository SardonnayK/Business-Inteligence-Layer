using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Orchestrator.Infrastructure.Data;

namespace Orchestrator.Api.Controllers;

[ApiController]
[Route("api/tenants")]
[Authorize]
public class TenantController : ControllerBase
{
    private readonly AppDbContext _db;

    public TenantController(AppDbContext db) => _db = db;

    /// <summary>Admins see all tenants; Members see only their own tenant.</summary>
    [HttpGet]
    public async Task<IActionResult> GetTenants(CancellationToken ct)
    {
        if (User.IsInRole("Admin"))
        {
            var all = await _db.Tenants.AsNoTracking()
                .OrderBy(t => t.Name)
                .Select(t => new { t.Id, t.Name, t.IsActive, t.CreatedAt })
                .ToListAsync(ct);
            return Ok(all);
        }

        var tenantId = Guid.Parse(User.FindFirstValue("tenant_id")!);
        var tenant = await _db.Tenants.AsNoTracking()
            .Where(t => t.Id == tenantId)
            .Select(t => new { t.Id, t.Name, t.IsActive, t.CreatedAt })
            .FirstOrDefaultAsync(ct);

        return Ok(tenant is null ? Array.Empty<object>() : new[] { (object)tenant });
    }

    /// <summary>List all projects for a given tenant.</summary>
    [HttpGet("{id:guid}/projects")]
    public async Task<IActionResult> GetProjects(Guid id, CancellationToken ct)
    {
        var tenantExists = await _db.Tenants.AnyAsync(t => t.Id == id, ct);
        if (!tenantExists) return NotFound();

        var projects = await _db.Projects
            .AsNoTracking()
            .Where(p => p.TenantId == id)
            .OrderBy(p => p.Name)
            .Select(p => new { p.Id, p.TenantId, p.Name, p.Description, p.CreatedAt })
            .ToListAsync(ct);

        return Ok(projects);
    }
}
