using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Orchestrator.Core.Entities;
using Orchestrator.Core.Interfaces;
using Orchestrator.Infrastructure.Data;

namespace Orchestrator.Api.Controllers;

[ApiController]
[Route("api/mcp-integrations")]
[Authorize]
public class McpIntegrationController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IMcpClientService _mcpClient;

    public McpIntegrationController(AppDbContext db, IMcpClientService mcpClient)
    {
        _db = db;
        _mcpClient = mcpClient;
    }

    private Guid GetTenantId() => Guid.Parse(User.FindFirstValue("tenant_id")!);

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var tenantId = GetTenantId();
        var items = await _db.McpIntegrations.AsNoTracking()
            .Where(m => m.TenantId == tenantId)
            .OrderBy(m => m.Name)
            .ToListAsync(ct);

        return Ok(items.Select(MapToResponse));
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create([FromBody] CreateMcpIntegrationRequest request, CancellationToken ct)
    {
        var tenantId = GetTenantId();
        var integration = new McpIntegration
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = request.Name,
            ServerUrl = request.ServerUrl,
            ApiKey = request.ApiKey,
            AuthHeaderName = request.AuthHeaderName ?? "Authorization",
            ToolNamesJson = request.ToolNames is { Length: > 0 }
                ? JsonSerializer.Serialize(request.ToolNames)
                : "[]",
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.McpIntegrations.Add(integration);
        await _db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(Get), new { id = integration.Id }, MapToResponse(integration));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var tenantId = GetTenantId();
        var integration = await _db.McpIntegrations.AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == id && m.TenantId == tenantId, ct);

        return integration is null ? NotFound() : Ok(MapToResponse(integration));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateMcpIntegrationRequest request, CancellationToken ct)
    {
        var tenantId = GetTenantId();
        var integration = await _db.McpIntegrations
            .FirstOrDefaultAsync(m => m.Id == id && m.TenantId == tenantId, ct);

        if (integration is null) return NotFound();

        if (request.Name is not null) integration.Name = request.Name;
        if (request.ServerUrl is not null) integration.ServerUrl = request.ServerUrl;
        if (request.ApiKey is not null) integration.ApiKey = request.ApiKey;
        if (request.AuthHeaderName is not null) integration.AuthHeaderName = request.AuthHeaderName;
        if (request.IsEnabled.HasValue) integration.IsEnabled = request.IsEnabled.Value;
        if (request.ToolNames is not null)
            integration.ToolNamesJson = JsonSerializer.Serialize(request.ToolNames);
        integration.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return Ok(MapToResponse(integration));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var tenantId = GetTenantId();
        var integration = await _db.McpIntegrations
            .FirstOrDefaultAsync(m => m.Id == id && m.TenantId == tenantId, ct);

        if (integration is null) return NotFound();

        _db.McpIntegrations.Remove(integration);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/discover")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Discover(Guid id, CancellationToken ct)
    {
        var tenantId = GetTenantId();
        var integration = await _db.McpIntegrations
            .FirstOrDefaultAsync(m => m.Id == id && m.TenantId == tenantId, ct);

        if (integration is null) return NotFound();

        try
        {
            var tools = await _mcpClient.ListToolsAsync(integration, ct);
            integration.CachedToolsJson = JsonSerializer.Serialize(tools);
            integration.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);

            return Ok(new
            {
                toolCount = tools.Count,
                tools = tools.Select(t => new { t.Name, t.Description })
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = $"Discovery failed: {ex.Message}" });
        }
    }

    private static object MapToResponse(McpIntegration m) => new
    {
        m.Id,
        m.TenantId,
        m.Name,
        m.ServerUrl,
        hasApiKey = !string.IsNullOrEmpty(m.ApiKey),
        m.AuthHeaderName,
        m.ToolNamesJson,
        m.CachedToolsJson,
        m.IsEnabled,
        m.CreatedAt,
        m.UpdatedAt
    };
}

public record CreateMcpIntegrationRequest(
    string Name,
    string ServerUrl,
    string? ApiKey,
    string? AuthHeaderName,
    string[]? ToolNames);

public record UpdateMcpIntegrationRequest(
    string? Name,
    string? ServerUrl,
    string? ApiKey,
    string? AuthHeaderName,
    bool? IsEnabled,
    string[]? ToolNames);
