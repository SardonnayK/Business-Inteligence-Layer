using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Orchestrator.Api.Auth;
using Orchestrator.Infrastructure.Data;

namespace Orchestrator.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly JwtTokenGenerator _jwt;

    public AuthController(AppDbContext db, JwtTokenGenerator jwt)
    {
        _db = db;
        _jwt = jwt;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var user = await _db.TenantUsers
            .FirstOrDefaultAsync(u =>
                u.TenantId == request.TenantId &&
                u.Username == request.Username &&
                u.IsActive, ct);

        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            return Unauthorized(new { message = "Invalid credentials." });

        var (token, expiresAt) = _jwt.Generate(user);

        return Ok(new
        {
            token,
            expiresAt,
            userId = user.Id,
            user.Username,
            tenantId = user.TenantId,
            role = user.Role.ToString()
        });
    }
}

public record LoginRequest(string Username, string Password, Guid TenantId);
