using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Orchestrator.Core.Enums;
using Orchestrator.Core.Interfaces;

namespace Orchestrator.Api.Controllers;

[ApiController]
[Route("api/business-context")]
[Authorize]
public class BusinessContextController : ControllerBase
{
    private readonly IHybridRagService _ragService;
    private readonly IArtifactSupervisorService _supervisorService;
    private readonly IPermissionService _permissions;

    public BusinessContextController(
        IHybridRagService ragService,
        IArtifactSupervisorService supervisorService,
        IPermissionService permissions)
    {
        _ragService = ragService;
        _supervisorService = supervisorService;
        _permissions = permissions;
    }

    private Guid GetTenantId() => Guid.Parse(User.FindFirstValue("tenant_id")!);
    private Guid GetUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private UserRole GetRole() => Enum.Parse<UserRole>(User.FindFirstValue(ClaimTypes.Role)!);

    /// <summary>Ingest a business context chunk — supervisor routes it to the correct artifact.</summary>
    [HttpPost]
    public async Task<IActionResult> Ingest(
        [FromBody] IngestRequest request,
        CancellationToken ct)
    {
        var tenantId = GetTenantId();
        var artifact = await _supervisorService.RouteToArtifactAsync(request.Text, tenantId, ct);

        var role = GetRole();
        if (role != UserRole.Admin)
        {
            var canWrite = await _permissions.CanWriteArtifactAsync(GetUserId(), role, artifact.Id, ct);
            if (!canWrite)
                return StatusCode(403, new { message = $"You do not have write permission on artifact '{artifact.Name}'." });
        }

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
        [FromQuery] string query,
        [FromQuery] int topK = 5,
        [FromQuery] Guid? artifactId = null,
        [FromQuery] Guid? departmentId = null,
        CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var userId = GetUserId();
        var role = GetRole();

        if (role != UserRole.Admin && artifactId.HasValue)
        {
            var canRead = await _permissions.CanReadArtifactAsync(userId, role, artifactId.Value, ct);
            if (!canRead)
                return StatusCode(403, new { message = "You do not have read permission on this artifact." });
        }

        var results = await _ragService.SearchAsync(query, tenantId, topK, artifactId, departmentId, ct);

        if (role != UserRole.Admin && !artifactId.HasValue)
        {
            var readableIds = await _permissions.GetReadableArtifactIdsAsync(userId, role, tenantId, ct);
            results = results.Where(r => r.ArtifactId == null || readableIds.Contains(r.ArtifactId.Value)).ToList();
        }

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
