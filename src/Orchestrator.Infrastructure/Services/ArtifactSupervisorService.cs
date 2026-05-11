using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Orchestrator.Core.Entities;
using Orchestrator.Core.Interfaces;
using Orchestrator.Infrastructure.Data;

namespace Orchestrator.Infrastructure.Services;

public class ArtifactSupervisorService : IArtifactSupervisorService
{
    private readonly AppDbContext _db;
    private readonly IEmbeddingProviderFactory _embeddingFactory;
    private readonly IChatProviderFactory _chatFactory;

    public ArtifactSupervisorService(
        AppDbContext db,
        IEmbeddingProviderFactory embeddingFactory,
        IChatProviderFactory chatFactory)
    {
        _db = db;
        _embeddingFactory = embeddingFactory;
        _chatFactory = chatFactory;
    }

    public async Task<Artifact> RouteToArtifactAsync(string text, Guid tenantId, CancellationToken ct = default)
    {
        // 1. Load the tenant's EmbeddingProviderConfig (tenant-specific → system default fallback)
        var config = await GetProviderConfigAsync(tenantId, ct);

        // 2. Try to get chat client — may be null
        IChatClient? chatClient = config is not null ? _chatFactory.GetChatClient(config) : null;

        // 3. Load all Artifacts for this tenant (include ArtifactDepartments → Department)
        var artifacts = await _db.Artifacts
            .Include(a => a.ArtifactDepartments).ThenInclude(ad => ad.Department)
            .Where(a => a.TenantId == tenantId)
            .ToListAsync(ct);

        // 4. If no artifacts exist → bootstrap with a shared artifact
        if (artifacts.Count == 0)
        {
            return await BootstrapAsync(tenantId, ct);
        }

        // 5. If chat client available → AI routing
        if (chatClient is not null)
        {
            return await AiRouteAsync(text, artifacts, chatClient, tenantId, ct);
        }

        // 6. No chat client → fall back to shared artifact
        return artifacts.FirstOrDefault(a => a.IsShared)
            ?? artifacts.First();
    }

    public async Task<DiscoveryResult> DiscoverDepartmentsAsync(Guid tenantId, CancellationToken ct = default)
    {
        // 1. Load up to 30 BusinessContext texts for this tenant
        var texts = await _db.BusinessContexts
            .AsNoTracking()
            .Where(bc => bc.TenantId == tenantId)
            .Take(30)
            .Select(bc => bc.Text)
            .ToListAsync(ct);

        // 2. Load provider config + get chat client
        var config = await GetProviderConfigAsync(tenantId, ct);
        IChatClient? chatClient = config is not null ? _chatFactory.GetChatClient(config) : null;

        // 3. No chat client → create single "General" department with one artifact
        if (chatClient is null)
        {
            var (dept, artifact, sharedArtifact) = await CreateGeneralDepartmentAsync(tenantId, ct);
            await _db.SaveChangesAsync(ct);

            var depts = dept is not null ? new List<Department> { dept } : new List<Department>();
            var arts = new List<Artifact>();
            if (artifact is not null) arts.Add(artifact);
            if (sharedArtifact is not null) arts.Add(sharedArtifact);

            return new DiscoveryResult
            {
                Departments = depts,
                Artifacts = arts,
                WasAiAssisted = false
            };
        }

        // 4. AI-assisted discovery
        return await AiDiscoverAsync(texts, tenantId, chatClient, ct);
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    private async Task<Artifact> BootstrapAsync(Guid tenantId, CancellationToken ct)
    {
        var sharedArtifact = new Artifact
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Company Knowledge",
            Description = "General company knowledge, mission, values, strategy, and cross-departmental policies.",
            IsShared = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.Artifacts.Add(sharedArtifact);
        await _db.SaveChangesAsync(ct);
        return sharedArtifact;
    }

    private async Task<Artifact> AiRouteAsync(
        string text,
        List<Artifact> artifacts,
        IChatClient chatClient,
        Guid tenantId,
        CancellationToken ct)
    {
        try
        {
            var manifest = await LoadManifestAsync(tenantId, ct);
            var artifactsJson = BuildArtifactsJson(artifacts);

            var manifestSection = manifest is not null
                ? "Department manifest for this tenant:\n" + manifest.Content + "\n\n"
                : string.Empty;

            var prompt =
                "You are a knowledge routing supervisor. Route the following text to the correct artifact.\n\n" +
                manifestSection +
                "Artifacts (JSON):\n" + artifactsJson + "\n\n" +
                "Text to route:\n\"" + text + "\"\n\n" +
                "Reply with ONLY valid JSON — pick one format:\n" +
                "{\"action\":\"route\",\"artifactId\":\"<uuid>\"}\n" +
                "{\"action\":\"create\",\"departmentName\":\"<name>\",\"artifactName\":\"<name>\",\"artifactDescription\":\"<one sentence>\"}\n" +
                "{\"action\":\"shared\"}";

            var response = await chatClient.GetResponseAsync(prompt, cancellationToken: ct);
            var responseText = response.Text;

            return await ParseAndApplyRouteResponseAsync(responseText, artifacts, tenantId, ct);
        }
        catch
        {
            // Fall back to shared artifact on any error
            return artifacts.FirstOrDefault(a => a.IsShared) ?? artifacts.First();
        }
    }

    private async Task<Artifact> ParseAndApplyRouteResponseAsync(
        string? responseText,
        List<Artifact> artifacts,
        Guid tenantId,
        CancellationToken ct)
    {
        var sharedArtifact = artifacts.FirstOrDefault(a => a.IsShared) ?? artifacts.First();

        if (string.IsNullOrWhiteSpace(responseText))
            return sharedArtifact;

        try
        {
            using var doc = JsonDocument.Parse(responseText.Trim());
            var root = doc.RootElement;

            if (!root.TryGetProperty("action", out var actionProp))
                return sharedArtifact;

            var action = actionProp.GetString();

            switch (action)
            {
                case "route":
                {
                    if (!root.TryGetProperty("artifactId", out var idProp))
                        return sharedArtifact;

                    var idStr = idProp.GetString();
                    if (!Guid.TryParse(idStr, out var artifactId))
                        return sharedArtifact;

                    return artifacts.FirstOrDefault(a => a.Id == artifactId) ?? sharedArtifact;
                }

                case "create":
                {
                    var departmentName = root.TryGetProperty("departmentName", out var dNameProp)
                        ? dNameProp.GetString() ?? "New Department"
                        : "New Department";
                    var artifactName = root.TryGetProperty("artifactName", out var aNameProp)
                        ? aNameProp.GetString() ?? "New Artifact"
                        : "New Artifact";
                    var artifactDesc = root.TryGetProperty("artifactDescription", out var aDescProp)
                        ? aDescProp.GetString() ?? string.Empty
                        : string.Empty;

                    // Find or create the department
                    var department = await _db.Departments
                        .FirstOrDefaultAsync(d => d.TenantId == tenantId && d.Name == departmentName, ct);

                    if (department is null)
                    {
                        department = new Department
                        {
                            Id = Guid.NewGuid(),
                            TenantId = tenantId,
                            Name = departmentName,
                            Description = $"Department for {departmentName}.",
                            EstimatedSize = "small",
                            CreatedAt = DateTime.UtcNow
                        };
                        _db.Departments.Add(department);
                    }

                    var newArtifact = new Artifact
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenantId,
                        Name = artifactName,
                        Description = artifactDesc,
                        IsShared = false,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    _db.Artifacts.Add(newArtifact);

                    var artifactDepartment = new ArtifactDepartment
                    {
                        ArtifactId = newArtifact.Id,
                        DepartmentId = department.Id,
                        CreatedAt = DateTime.UtcNow
                    };
                    _db.ArtifactDepartments.Add(artifactDepartment);
                    await _db.SaveChangesAsync(ct);

                    artifactDepartment.Department = department;
                    newArtifact.ArtifactDepartments.Add(artifactDepartment);
                    return newArtifact;
                }

                case "shared":
                    return sharedArtifact;

                default:
                    return sharedArtifact;
            }
        }
        catch
        {
            return sharedArtifact;
        }
    }

    private async Task<DiscoveryResult> AiDiscoverAsync(
        List<string> texts,
        Guid tenantId,
        IChatClient chatClient,
        CancellationToken ct)
    {
        var createdDepartments = new List<Department>();
        var createdArtifacts = new List<Artifact>();

        try
        {
            var manifest = await LoadManifestAsync(tenantId, ct);
            var sampleText = string.Join("\n---\n", texts);

            var sizingGuidance = manifest is not null
                ? "Use this tenant department manifest to guide department naming and artifact sizing:\n" + manifest.Content + "\n\n"
                : "For each department specify how many artifacts (knowledge documents) it needs based on complexity:\n" +
                  "- small (focused): 1 artifact\n" +
                  "- medium: 2 artifacts\n" +
                  "- large (broad): 3 artifacts\n\n";

            var prompt =
                "Analyze these knowledge base text samples and identify the organizational departments.\n" +
                sizingGuidance +
                "Samples:\n" + sampleText + "\n\n" +
                "Reply with ONLY valid JSON:\n" +
                "{\"departments\":[{\"name\":\"Engineering\",\"description\":\"Technical knowledge and processes\",\"estimatedSize\":\"large\",\"artifacts\":[{\"name\":\"Technical Standards\",\"description\":\"Coding and architecture guidelines\"},{\"name\":\"Engineering Processes\",\"description\":\"Deployment, incidents, sprint rituals\"}]}],\"sharedArtifact\":{\"name\":\"Company Knowledge\",\"description\":\"Mission, values, strategy, cross-departmental policies\"}}";

            var response = await chatClient.GetResponseAsync(prompt, cancellationToken: ct);
            var responseText = response.Text;

            if (!string.IsNullOrWhiteSpace(responseText))
            {
                using var doc = JsonDocument.Parse(responseText.Trim());
                var root = doc.RootElement;

                // Process departments
                if (root.TryGetProperty("departments", out var departmentsEl) &&
                    departmentsEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var deptEl in departmentsEl.EnumerateArray())
                    {
                        var deptName = deptEl.TryGetProperty("name", out var np) ? np.GetString() ?? string.Empty : string.Empty;
                        var deptDesc = deptEl.TryGetProperty("description", out var dp) ? dp.GetString() ?? string.Empty : string.Empty;
                        var estimatedSize = deptEl.TryGetProperty("estimatedSize", out var sp) ? sp.GetString() ?? "small" : "small";

                        if (string.IsNullOrWhiteSpace(deptName)) continue;

                        // Skip if department already exists
                        var existingDept = await _db.Departments
                            .FirstOrDefaultAsync(d => d.TenantId == tenantId && d.Name == deptName, ct);

                        Department department;
                        if (existingDept is null)
                        {
                            department = new Department
                            {
                                Id = Guid.NewGuid(),
                                TenantId = tenantId,
                                Name = deptName,
                                Description = deptDesc,
                                EstimatedSize = estimatedSize,
                                CreatedAt = DateTime.UtcNow
                            };
                            _db.Departments.Add(department);
                            createdDepartments.Add(department);
                        }
                        else
                        {
                            department = existingDept;
                        }

                        // Process artifacts for this department
                        if (deptEl.TryGetProperty("artifacts", out var artifactsEl) &&
                            artifactsEl.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var artEl in artifactsEl.EnumerateArray())
                            {
                                var artName = artEl.TryGetProperty("name", out var an) ? an.GetString() ?? string.Empty : string.Empty;
                                var artDesc = artEl.TryGetProperty("description", out var ad) ? ad.GetString() ?? string.Empty : string.Empty;

                                if (string.IsNullOrWhiteSpace(artName)) continue;

                                // Skip if artifact already exists for this tenant with the same name
                                var existingArt = await _db.Artifacts
                                    .FirstOrDefaultAsync(a => a.TenantId == tenantId && a.Name == artName, ct);

                                if (existingArt is null)
                                {
                                    var artifact = new Artifact
                                    {
                                        Id = Guid.NewGuid(),
                                        TenantId = tenantId,
                                        Name = artName,
                                        Description = artDesc,
                                        IsShared = false,
                                        CreatedAt = DateTime.UtcNow,
                                        UpdatedAt = DateTime.UtcNow
                                    };
                                    _db.Artifacts.Add(artifact);

                                    var artifactDept = new ArtifactDepartment
                                    {
                                        ArtifactId = artifact.Id,
                                        DepartmentId = department.Id,
                                        CreatedAt = DateTime.UtcNow
                                    };
                                    _db.ArtifactDepartments.Add(artifactDept);
                                    createdArtifacts.Add(artifact);
                                }
                            }
                        }
                    }
                }

                // Always upsert shared artifact
                var sharedArtifact = await UpsertSharedArtifactAsync(tenantId, root, ct);
                if (!createdArtifacts.Any(a => a.IsShared))
                    createdArtifacts.Add(sharedArtifact);
            }
        }
        catch
        {
            // On failure, ensure at least a shared artifact exists
            var sharedArtifact = await EnsureSharedArtifactAsync(tenantId, ct);
            if (!createdArtifacts.Any(a => a.IsShared))
                createdArtifacts.Add(sharedArtifact);
        }

        await _db.SaveChangesAsync(ct);

        return new DiscoveryResult
        {
            Departments = createdDepartments,
            Artifacts = createdArtifacts,
            WasAiAssisted = true
        };
    }

    private async Task<Artifact> UpsertSharedArtifactAsync(Guid tenantId, JsonElement root, CancellationToken ct)
    {
        string sharedName = "Company Knowledge";
        string sharedDesc = "Mission, values, strategy, cross-departmental policies.";

        if (root.TryGetProperty("sharedArtifact", out var sharedEl))
        {
            if (sharedEl.TryGetProperty("name", out var sn) && sn.GetString() is { } sName)
                sharedName = sName;
            if (sharedEl.TryGetProperty("description", out var sd) && sd.GetString() is { } sDesc)
                sharedDesc = sDesc;
        }

        var existing = await _db.Artifacts
            .FirstOrDefaultAsync(a => a.TenantId == tenantId && a.IsShared, ct);

        if (existing is not null)
        {
            existing.Name = sharedName;
            existing.Description = sharedDesc;
            existing.UpdatedAt = DateTime.UtcNow;
            return existing;
        }

        var newShared = new Artifact
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = sharedName,
            Description = sharedDesc,
            IsShared = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.Artifacts.Add(newShared);
        return newShared;
    }

    private async Task<Artifact> EnsureSharedArtifactAsync(Guid tenantId, CancellationToken ct)
    {
        var existing = await _db.Artifacts
            .FirstOrDefaultAsync(a => a.TenantId == tenantId && a.IsShared, ct);

        if (existing is not null)
            return existing;

        var newShared = new Artifact
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Company Knowledge",
            Description = "Mission, values, strategy, cross-departmental policies.",
            IsShared = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.Artifacts.Add(newShared);
        return newShared;
    }

    private async Task<(Department? dept, Artifact? artifact, Artifact? sharedArtifact)> CreateGeneralDepartmentAsync(
        Guid tenantId, CancellationToken ct)
    {
        Department? dept = null;
        Artifact? artifact = null;

        var existingDept = await _db.Departments
            .FirstOrDefaultAsync(d => d.TenantId == tenantId && d.Name == "General", ct);

        if (existingDept is null)
        {
            dept = new Department
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = "General",
                Description = "General knowledge and processes.",
                EstimatedSize = "small",
                CreatedAt = DateTime.UtcNow
            };
            _db.Departments.Add(dept);

            artifact = new Artifact
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = "General Knowledge",
                Description = "General organizational knowledge.",
                IsShared = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _db.Artifacts.Add(artifact);

            var generalArtifactDept = new ArtifactDepartment
            {
                ArtifactId = artifact.Id,
                DepartmentId = dept.Id,
                CreatedAt = DateTime.UtcNow
            };
            _db.ArtifactDepartments.Add(generalArtifactDept);
        }

        var sharedArtifact = await EnsureSharedArtifactAsync(tenantId, ct);

        return (dept, artifact, sharedArtifact);
    }

    private async Task<DepartmentManifest?> LoadManifestAsync(Guid tenantId, CancellationToken ct)
    {
        return await _db.DepartmentManifests
            .AsNoTracking()
            .FirstOrDefaultAsync(dm => dm.TenantId == tenantId, ct);
    }

    private async Task<EmbeddingProviderConfig?> GetProviderConfigAsync(Guid tenantId, CancellationToken ct)
    {
        var config = await _db.EmbeddingProviderConfigs
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.TenantId == tenantId, ct);

        if (config is null)
        {
            config = await _db.EmbeddingProviderConfigs
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.TenantId == null, ct);
        }

        return config;
    }

    private static string BuildArtifactsJson(List<Artifact> artifacts)
    {
        var sb = new StringBuilder();
        sb.Append('[');
        for (int i = 0; i < artifacts.Count; i++)
        {
            var a = artifacts[i];
            sb.Append('{');
            sb.Append($"\"id\":\"{a.Id}\"");
            sb.Append($",\"name\":{JsonSerializer.Serialize(a.Name)}");
            sb.Append($",\"description\":{JsonSerializer.Serialize(a.Description)}");
            sb.Append($",\"isShared\":{(a.IsShared ? "true" : "false")}");
            sb.Append($",\"department\":{JsonSerializer.Serialize(a.ArtifactDepartments.FirstOrDefault()?.Department?.Name ?? string.Empty)}");
            sb.Append('}');
            if (i < artifacts.Count - 1) sb.Append(',');
        }
        sb.Append(']');
        return sb.ToString();
    }
}
