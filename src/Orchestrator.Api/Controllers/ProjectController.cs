using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Orchestrator.Infrastructure.Data;

namespace Orchestrator.Api.Controllers;

[ApiController]
[Route("api/projects")]
public class ProjectController : ControllerBase
{
    private readonly AppDbContext _db;

    public ProjectController(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>List all requirements for a given project.</summary>
    [HttpGet("{id:guid}/requirements")]
    public async Task<IActionResult> GetRequirements(Guid id, CancellationToken ct)
    {
        var projectExists = await _db.Projects.AnyAsync(p => p.Id == id, ct);
        if (!projectExists)
            return NotFound();

        var requirements = await _db.Requirements
            .AsNoTracking()
            .Where(r => r.ProjectId == id)
            .OrderBy(r => r.CreatedAt)
            .Select(r => new { r.Id, r.ProjectId, r.Content, r.Status, r.CreatedAt })
            .ToListAsync(ct);

        return Ok(requirements);
    }
}
