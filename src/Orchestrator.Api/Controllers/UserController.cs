using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Orchestrator.Core.Entities;
using Orchestrator.Core.Enums;
using Orchestrator.Infrastructure.Data;

namespace Orchestrator.Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize(Roles = "Admin")]
public class UserController : ControllerBase
{
    private readonly AppDbContext _db;

    public UserController(AppDbContext db) => _db = db;

    private Guid GetTenantId() => Guid.Parse(User.FindFirstValue("tenant_id")!);
    private Guid GetUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<IActionResult> GetUsers(CancellationToken ct)
    {
        var tenantId = GetTenantId();
        var users = await _db.TenantUsers
            .AsNoTracking()
            .Where(u => u.TenantId == tenantId)
            .Select(u => new
            {
                u.Id,
                u.Username,
                role = u.Role.ToString(),
                u.IsActive,
                u.CreatedAt,
                permissionCount = _db.ArtifactPermissions.Count(ap => ap.UserId == u.Id)
            })
            .ToListAsync(ct);

        return Ok(users);
    }

    [HttpPost]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request, CancellationToken ct)
    {
        var tenantId = GetTenantId();

        if (await _db.TenantUsers.AnyAsync(u => u.TenantId == tenantId && u.Username == request.Username, ct))
            return Conflict(new { message = $"Username '{request.Username}' already exists in this tenant." });

        if (!Enum.TryParse<UserRole>(request.Role, ignoreCase: true, out var role))
            return BadRequest(new { message = "Role must be 'Admin' or 'Member'." });

        var user = new TenantUser
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Username = request.Username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password, workFactor: 10),
            Role = role,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _db.TenantUsers.Add(user);
        await _db.SaveChangesAsync(ct);

        return Ok(new { user.Id, user.Username, role = user.Role.ToString(), user.IsActive, user.CreatedAt });
    }

    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> UpdateUser(Guid id, [FromBody] UpdateUserRequest request, CancellationToken ct)
    {
        var tenantId = GetTenantId();
        var user = await _db.TenantUsers
            .FirstOrDefaultAsync(u => u.Id == id && u.TenantId == tenantId, ct);

        if (user is null)
            return NotFound(new { message = "User not found." });

        if (id == GetUserId() && request.Role is not null)
            return BadRequest(new { message = "Cannot change your own role." });

        if (request.Role is not null)
        {
            if (!Enum.TryParse<UserRole>(request.Role, ignoreCase: true, out var role))
                return BadRequest(new { message = "Role must be 'Admin' or 'Member'." });
            user.Role = role;
        }

        if (request.IsActive.HasValue)
            user.IsActive = request.IsActive.Value;

        await _db.SaveChangesAsync(ct);
        return Ok(new { user.Id, user.Username, role = user.Role.ToString(), user.IsActive, user.CreatedAt });
    }

    [HttpGet("{id:guid}/permissions")]
    public async Task<IActionResult> GetUserPermissions(Guid id, CancellationToken ct)
    {
        var tenantId = GetTenantId();

        if (!await _db.TenantUsers.AnyAsync(u => u.Id == id && u.TenantId == tenantId, ct))
            return NotFound(new { message = "User not found." });

        var artifacts = await _db.Artifacts
            .AsNoTracking()
            .Include(a => a.Department)
            .Where(a => a.TenantId == tenantId)
            .ToListAsync(ct);

        var permissions = await _db.ArtifactPermissions
            .AsNoTracking()
            .Where(ap => ap.UserId == id)
            .ToListAsync(ct);

        var result = artifacts.Select(a =>
        {
            var perm = permissions.FirstOrDefault(p => p.ArtifactId == a.Id);
            return new
            {
                a.Id,
                a.Name,
                departmentName = a.Department?.Name,
                a.IsShared,
                canRead = perm?.CanRead ?? false,
                canWrite = perm?.CanWrite ?? false
            };
        });

        return Ok(result);
    }

    [HttpPut("{id:guid}/permissions")]
    public async Task<IActionResult> UpdateUserPermissions(
        Guid id,
        [FromBody] List<PermissionEntry> entries,
        CancellationToken ct)
    {
        var tenantId = GetTenantId();

        if (!await _db.TenantUsers.AnyAsync(u => u.Id == id && u.TenantId == tenantId, ct))
            return NotFound(new { message = "User not found." });

        var existing = await _db.ArtifactPermissions
            .Where(ap => ap.UserId == id)
            .ToListAsync(ct);
        _db.ArtifactPermissions.RemoveRange(existing);

        var artifactIds = await _db.Artifacts
            .Where(a => a.TenantId == tenantId)
            .Select(a => a.Id)
            .ToListAsync(ct);

        foreach (var entry in entries.Where(e => artifactIds.Contains(e.ArtifactId)))
        {
            _db.ArtifactPermissions.Add(new ArtifactPermission
            {
                Id = Guid.NewGuid(),
                UserId = id,
                ArtifactId = entry.ArtifactId,
                CanRead = entry.CanRead,
                CanWrite = entry.CanWrite,
                CreatedAt = DateTime.UtcNow
            });
        }

        await _db.SaveChangesAsync(ct);
        return NoContent();
    }
}

public record CreateUserRequest(string Username, string Password, string Role);
public record UpdateUserRequest(string? Role, bool? IsActive);
public record PermissionEntry(Guid ArtifactId, bool CanRead, bool CanWrite);
