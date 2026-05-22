using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Orchestrator.Core.Entities;
using Orchestrator.Core.Enums;
using Orchestrator.Core.Interfaces;
using Orchestrator.Infrastructure.Data;

namespace Orchestrator.Api.Controllers;

[ApiController]
[Authorize]
public class AgentController : ControllerBase
{
    private readonly ISupervisorAgent _supervisor;
    private readonly AppDbContext _db;

    public AgentController(ISupervisorAgent supervisor, AppDbContext db)
    {
        _supervisor = supervisor;
        _db = db;
    }

    private Guid GetTenantId() => Guid.Parse(User.FindFirstValue("tenant_id")!);
    private Guid GetUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private UserRole GetRole() => Enum.TryParse<UserRole>(User.FindFirstValue("role"), out var r) ? r : UserRole.Member;

    // ── Main process endpoint ────────────────────────────────────────────────

    [HttpPost("api/agent/process")]
    public async Task<IActionResult> Process(CancellationToken ct)
    {
        var tenantId = GetTenantId();
        var userId = GetUserId();
        var role = GetRole();

        SupervisorRequest request;

        if (Request.HasFormContentType)
        {
            var form = await Request.ReadFormAsync(ct);
            if (!int.TryParse(form["capability"], out var cap))
                return BadRequest(new { message = "capability is required." });

            IFormFile? file = form.Files.GetFile("file");
            byte[]? fileBytes = null;
            string? fileName = null;
            string? mimeType = null;

            if (file is not null)
            {
                using var ms = new MemoryStream();
                await file.CopyToAsync(ms, ct);
                fileBytes = ms.ToArray();
                fileName = file.FileName;
                mimeType = file.ContentType;
            }

            Guid.TryParse(form["hintArtifactId"], out var hintId);
            Guid.TryParse(form["forceRegistrationId"], out var forceId);

            request = new SupervisorRequest(
                TenantId: tenantId,
                UserId: userId,
                UserRole: role,
                Capability: (AgentCapability)cap,
                TextInput: form["textInput"].ToString() is { Length: > 0 } t ? t : null,
                FileBytes: fileBytes,
                FileName: fileName,
                MimeType: mimeType,
                HintArtifactId: hintId == Guid.Empty ? null : hintId,
                ForceRegistrationId: forceId == Guid.Empty ? null : forceId);
        }
        else
        {
            var body = await System.Text.Json.JsonSerializer.DeserializeAsync<ProcessRequestBody>(
                Request.Body,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true },
                ct);

            if (body is null)
                return BadRequest(new { message = "Request body is required." });

            request = new SupervisorRequest(
                TenantId: tenantId,
                UserId: userId,
                UserRole: role,
                Capability: (AgentCapability)body.Capability,
                TextInput: body.TextInput,
                FileBytes: null,
                FileName: null,
                MimeType: null,
                HintArtifactId: body.HintArtifactId,
                Source: body.Source,
                Category: body.Category,
                ForceRegistrationId: body.ForceRegistrationId);
        }

        var result = await _supervisor.ProcessAsync(request, ct);
        return Ok(MapResult(result));
    }

    [HttpPost("api/agent/process/{executionId:guid}/confirm")]
    public async Task<IActionResult> Confirm(Guid executionId, [FromBody] ConfirmRequest body, CancellationToken ct)
    {
        var result = await _supervisor.ConfirmAsync(executionId, body.Accept, ct);
        return result.Success ? Ok(MapResult(result)) : BadRequest(MapResult(result));
    }

    // ── Agent registrations ──────────────────────────────────────────────────

    [HttpGet("api/agents")]
    public async Task<IActionResult> GetAgents(CancellationToken ct)
    {
        var tenantId = GetTenantId();
        var registrations = await _db.AgentRegistrations
            .AsNoTracking()
            .Where(r => r.TenantId == tenantId || r.TenantId == null)
            .OrderByDescending(r => r.TenantId != null)
            .ThenByDescending(r => r.Priority)
            .ToListAsync(ct);

        return Ok(registrations.Select(MapRegistration));
    }

    [HttpPost("api/agents")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreateAgent([FromBody] CreateAgentRequest body, CancellationToken ct)
    {
        var tenantId = GetTenantId();

        if (body.AgentType != AgentType.HttpPlugin)
            return BadRequest(new { message = "Only HttpPlugin agents can be registered via this endpoint." });

        if (string.IsNullOrWhiteSpace(body.HttpEndpoint))
            return BadRequest(new { message = "httpEndpoint is required for HttpPlugin agents." });

        var reg = new AgentRegistration
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            AgentType = body.AgentType,
            Capability = (AgentCapability)body.Capability,
            Name = body.Name,
            Description = body.Description ?? string.Empty,
            HttpEndpoint = body.HttpEndpoint,
            InputSchemaJson = body.InputSchemaJson,
            IsEnabled = true,
            Priority = body.Priority ?? 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.AgentRegistrations.Add(reg);
        await _db.SaveChangesAsync(ct);
        return Ok(MapRegistration(reg));
    }

    [HttpPut("api/agents/{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateAgent(Guid id, [FromBody] UpdateAgentRequest body, CancellationToken ct)
    {
        var tenantId = GetTenantId();
        var reg = await _db.AgentRegistrations
            .FirstOrDefaultAsync(r => r.Id == id && r.TenantId == tenantId, ct);

        if (reg is null)
            return NotFound(new { message = $"Agent registration '{id}' not found for your tenant." });

        if (body.IsEnabled.HasValue) reg.IsEnabled = body.IsEnabled.Value;
        if (body.Priority.HasValue) reg.Priority = body.Priority.Value;
        if (body.HttpEndpoint is not null) reg.HttpEndpoint = body.HttpEndpoint;
        if (body.InputSchemaJson is not null) reg.InputSchemaJson = body.InputSchemaJson;
        reg.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return Ok(MapRegistration(reg));
    }

    [HttpDelete("api/agents/{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteAgent(Guid id, CancellationToken ct)
    {
        var tenantId = GetTenantId();
        var reg = await _db.AgentRegistrations
            .FirstOrDefaultAsync(r => r.Id == id && r.TenantId == tenantId, ct);

        if (reg is null)
            return NotFound(new { message = $"Agent registration '{id}' not found for your tenant." });

        if (reg.AgentType == AgentType.BuiltIn)
            return BadRequest(new { message = "Built-in agent registrations cannot be deleted." });

        _db.AgentRegistrations.Remove(reg);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    // ── Supervisor config ────────────────────────────────────────────────────

    [HttpGet("api/agents/config")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetConfig(CancellationToken ct)
    {
        var tenantId = GetTenantId();
        var tenant = await _db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == tenantId, ct);
        if (tenant is null) return NotFound();

        if (string.IsNullOrEmpty(tenant.AgentConfigJson))
            return Ok(new { requireConfirmationForRerouting = false, autoIngestAgentOutput = true, allowAutoCreateArtifacts = true });

        return Ok(System.Text.Json.JsonSerializer.Deserialize<object>(tenant.AgentConfigJson));
    }

    [HttpPut("api/agents/config")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateConfig([FromBody] AgentConfigRequest body, CancellationToken ct)
    {
        var tenantId = GetTenantId();
        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId, ct);
        if (tenant is null) return NotFound();

        tenant.AgentConfigJson = System.Text.Json.JsonSerializer.Serialize(new
        {
            requireConfirmationForRerouting = body.RequireConfirmationForRerouting,
            autoIngestAgentOutput = body.AutoIngestAgentOutput,
            allowAutoCreateArtifacts = body.AllowAutoCreateArtifacts
        });
        await _db.SaveChangesAsync(ct);

        return Ok(System.Text.Json.JsonSerializer.Deserialize<object>(tenant.AgentConfigJson));
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static object MapResult(SupervisorResult r) => new
    {
        r.ExecutionId,
        r.AgentName,
        r.Output,
        r.RoutedArtifactId,
        r.RequiresUserConfirmation,
        r.ConfirmationMessage,
        r.SuggestedArtifactId,
        ingestedCount = r.IngestedItems.Count,
        r.Success
    };

    private static object MapRegistration(AgentRegistration r) => new
    {
        r.Id,
        r.TenantId,
        agentType = r.AgentType.ToString(),
        capability = r.Capability.ToString(),
        r.Name,
        r.Description,
        r.HttpEndpoint,
        r.InputSchemaJson,
        r.IsEnabled,
        r.Priority,
        r.CreatedAt,
        r.UpdatedAt
    };
}

public record ProcessRequestBody(
    int Capability,
    string? TextInput,
    Guid? HintArtifactId,
    string? Source,
    string? Category,
    Guid? ForceRegistrationId);

public record ConfirmRequest(bool Accept);

public record CreateAgentRequest(
    AgentType AgentType,
    int Capability,
    string Name,
    string? Description,
    string? HttpEndpoint,
    string? InputSchemaJson,
    int? Priority);

public record UpdateAgentRequest(
    bool? IsEnabled,
    int? Priority,
    string? HttpEndpoint,
    string? InputSchemaJson);

public record AgentConfigRequest(
    bool RequireConfirmationForRerouting,
    bool AutoIngestAgentOutput,
    bool AllowAutoCreateArtifacts);
