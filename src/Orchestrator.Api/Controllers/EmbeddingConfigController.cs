using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Orchestrator.Core.Entities;
using Orchestrator.Core.Enums;
using Orchestrator.Infrastructure.Data;

namespace Orchestrator.Api.Controllers;

[ApiController]
[Route("api/embedding-config")]
[Authorize]
public class EmbeddingConfigController : ControllerBase
{
    private readonly AppDbContext _db;

    public EmbeddingConfigController(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>Returns the list of supported embedding providers with their required fields and supported models.</summary>
    [HttpGet("providers")]
    public IActionResult GetProviders()
    {
        var providers = new[]
        {
            new
            {
                type = "openai",
                name = "OpenAI",
                requiresApiKey = true,
                requiresEndpoint = false,
                defaultModel = "text-embedding-ada-002",
                supportedModels = new[] { "text-embedding-ada-002", "text-embedding-3-small", "text-embedding-3-large" }
            },
            new
            {
                type = "ollama",
                name = "Ollama (Local)",
                requiresApiKey = false,
                requiresEndpoint = true,
                defaultModel = "nomic-embed-text",
                supportedModels = new[] { "nomic-embed-text", "mxbai-embed-large", "all-minilm", "bge-m3" }
            },
            new
            {
                type = "azure-openai",
                name = "Azure OpenAI",
                requiresApiKey = true,
                requiresEndpoint = true,
                defaultModel = "text-embedding-ada-002",
                supportedModels = new[] { "text-embedding-ada-002", "text-embedding-3-small", "text-embedding-3-large" }
            },
            new
            {
                type = "docker-model-runner",
                name = "Docker Model Runner",
                requiresApiKey = false,
                requiresEndpoint = false,
                defaultModel = "ai/all-minilm",
                supportedModels = new[] { "ai/all-minilm", "ai/mxbai-embed-large", "ai/nomic-embed-text" }
            },
            new
            {
                type = "none",
                name = "None",
                requiresApiKey = false,
                requiresEndpoint = false,
                defaultModel = string.Empty,
                supportedModels = Array.Empty<string>()
            }
        };

        return Ok(providers);
    }

    /// <summary>Get the system-default embedding provider config (TenantId IS NULL).</summary>
    [HttpGet("system")]
    public async Task<IActionResult> GetSystemConfig(CancellationToken ct)
    {
        var config = await _db.EmbeddingProviderConfigs
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.TenantId == null, ct);

        if (config is null)
            return NotFound(new { message = "No system default embedding provider has been configured." });

        return Ok(MapToResponse(config, isDefault: false));
    }

    /// <summary>Upsert the system-default embedding provider config (TenantId IS NULL).</summary>
    [HttpPut("system")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpsertSystemConfig(
        [FromBody] EmbeddingConfigRequest request,
        CancellationToken ct)
    {
        var config = await _db.EmbeddingProviderConfigs
            .FirstOrDefaultAsync(e => e.TenantId == null, ct);

        if (config is null)
        {
            config = new EmbeddingProviderConfig { Id = Guid.NewGuid() };
            _db.EmbeddingProviderConfigs.Add(config);
        }

        ApplyRequest(config, request, tenantId: null);
        await _db.SaveChangesAsync(ct);

        return Ok(MapToResponse(config, isDefault: false));
    }

    /// <summary>Get the embedding provider config for a specific tenant; falls back to system default.</summary>
    [HttpGet("tenant/{id:guid}")]
    public async Task<IActionResult> GetTenantConfig(Guid id, CancellationToken ct)
    {
        var config = await _db.EmbeddingProviderConfigs
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.TenantId == id, ct);

        if (config is not null)
            return Ok(MapToResponse(config, isDefault: false));

        // Fall back to system default
        var systemConfig = await _db.EmbeddingProviderConfigs
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.TenantId == null, ct);

        if (systemConfig is null)
            return NotFound(new { message = "No tenant-specific config and no system default has been configured." });

        return Ok(MapToResponse(systemConfig, isDefault: true));
    }

    /// <summary>Upsert the embedding provider config for a specific tenant.</summary>
    [HttpPut("tenant/{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpsertTenantConfig(
        Guid id,
        [FromBody] EmbeddingConfigRequest request,
        CancellationToken ct)
    {
        var config = await _db.EmbeddingProviderConfigs
            .FirstOrDefaultAsync(e => e.TenantId == id, ct);

        if (config is null)
        {
            config = new EmbeddingProviderConfig { Id = Guid.NewGuid() };
            _db.EmbeddingProviderConfigs.Add(config);
        }

        ApplyRequest(config, request, tenantId: id);
        await _db.SaveChangesAsync(ct);

        return Ok(MapToResponse(config, isDefault: false));
    }

    /// <summary>Remove a tenant-specific embedding provider override (tenant reverts to system default).</summary>
    [HttpDelete("tenant/{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteTenantConfig(Guid id, CancellationToken ct)
    {
        var config = await _db.EmbeddingProviderConfigs
            .FirstOrDefaultAsync(e => e.TenantId == id, ct);

        if (config is null)
            return NotFound(new { message = $"No tenant-specific config found for tenant '{id}'." });

        _db.EmbeddingProviderConfigs.Remove(config);
        await _db.SaveChangesAsync(ct);

        return NoContent();
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static void ApplyRequest(EmbeddingProviderConfig config, EmbeddingConfigRequest request, Guid? tenantId)
    {
        config.TenantId = tenantId;
        config.ProviderType = (EmbeddingProviderType)request.ProviderType;
        config.ModelId = request.ModelId ?? string.Empty;
        config.Endpoint = request.Endpoint;
        config.ChatModelId = request.ChatModelId;
        config.UpdatedAt = DateTime.UtcNow;

        // Only overwrite the stored key if a new one is explicitly supplied
        if (request.ApiKey is not null)
            config.ApiKey = request.ApiKey;
    }

    private static object MapToResponse(EmbeddingProviderConfig config, bool isDefault) => new
    {
        config.Id,
        config.TenantId,
        config.ProviderType,
        config.ModelId,
        config.Endpoint,
        config.ChatModelId,
        hasApiKey = !string.IsNullOrEmpty(config.ApiKey),
        config.UpdatedAt,
        isDefault
    };
}

public record EmbeddingConfigRequest(
    int ProviderType,
    string? ModelId,
    string? ApiKey,
    string? Endpoint,
    string? ChatModelId);
