using Microsoft.AspNetCore.Mvc;
using Orchestrator.Core.Interfaces;

namespace Orchestrator.Api.Controllers;

[ApiController]
[Route("api/business-context")]
public class BusinessContextController : ControllerBase
{
    private readonly IHybridRagService _ragService;
    private readonly IArtifactSupervisorService _supervisorService;

    public BusinessContextController(IHybridRagService ragService, IArtifactSupervisorService supervisorService)
    {
        _ragService = ragService;
        _supervisorService = supervisorService;
    }

    /// <summary>Ingest a business context chunk — supervisor routes it to the correct artifact.</summary>
    [HttpPost]
    public async Task<IActionResult> Ingest(
        [FromHeader(Name = "X-Tenant-Id")] Guid tenantId,
        [FromBody] IngestRequest request,
        CancellationToken ct)
    {
        var artifact = await _supervisorService.RouteToArtifactAsync(request.Text, tenantId, ct);
        var result = await _ragService.IngestAsync(request.Text, tenantId, artifact.Id, request.Source, request.Category, ct);

        return Ok(new
        {
            result.Id,
            result.TenantId,
            artifactId = artifact.Id,
            artifactName = artifact.Name,
            departmentId = artifact.DepartmentId,
            departmentName = artifact.Department?.Name,
            isShared = artifact.IsShared,
            result.CreatedAt
        });
    }

    /// <summary>Perform a hybrid (relational + vector) search scoped to the tenant, optionally filtered by artifact or department.</summary>
    [HttpGet("search")]
    public async Task<IActionResult> Search(
        [FromHeader(Name = "X-Tenant-Id")] Guid tenantId,
        [FromQuery] string query,
        [FromQuery] int topK = 5,
        [FromQuery] Guid? artifactId = null,
        [FromQuery] Guid? departmentId = null,
        CancellationToken ct = default)
    {
        var results = await _ragService.SearchAsync(query, tenantId, topK, artifactId, departmentId, ct);
        return Ok(results.Select(r => new
        {
            r.Id,
            r.Text,
            r.Source,
            r.Category,
            r.ArtifactId,
            artifactName = r.Artifact?.Name,
            r.CreatedAt
        }));
    }
}

public record IngestRequest(string Text, string? Source, string? Category);
