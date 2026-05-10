using Microsoft.AspNetCore.Mvc;
using Orchestrator.Core.Interfaces;

namespace Orchestrator.Api.Controllers;

[ApiController]
[Route("api/business-context")]
public class BusinessContextController : ControllerBase
{
    private readonly IHybridRagService _ragService;

    public BusinessContextController(IHybridRagService ragService)
    {
        _ragService = ragService;
    }

    /// <summary>Ingest a business context chunk and store its embedding.</summary>
    [HttpPost]
    public async Task<IActionResult> Ingest(
        [FromHeader(Name = "X-Tenant-Id")] Guid tenantId,
        [FromBody] IngestRequest request,
        CancellationToken ct)
    {
        var result = await _ragService.IngestAsync(request.Text, tenantId, request.Source, request.Category, ct);
        return Ok(new { result.Id, result.TenantId, result.CreatedAt });
    }

    /// <summary>Perform a hybrid (relational + vector) search scoped to the tenant.</summary>
    [HttpGet("search")]
    public async Task<IActionResult> Search(
        [FromHeader(Name = "X-Tenant-Id")] Guid tenantId,
        [FromQuery] string query,
        [FromQuery] int topK = 5,
        CancellationToken ct = default)
    {
        var results = await _ragService.SearchAsync(query, tenantId, topK, ct);
        return Ok(results.Select(r => new { r.Id, r.Text, r.Source, r.Category, r.CreatedAt }));
    }
}

public record IngestRequest(string Text, string? Source, string? Category);
