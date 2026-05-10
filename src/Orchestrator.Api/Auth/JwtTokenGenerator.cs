using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Orchestrator.Core.Entities;

namespace Orchestrator.Api.Auth;

public sealed class JwtTokenGenerator
{
    private readonly IConfiguration _config;

    public JwtTokenGenerator(IConfiguration config) => _config = config;

    public (string Token, DateTime ExpiresAt) Generate(TenantUser user)
    {
        var secret = _config["Jwt:Secret"]
            ?? "dev-secret-key-change-in-production-must-be-32-chars!";
        var expiresInHours = int.TryParse(_config["Jwt:ExpiresInHours"], out var h) ? h : 24;
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expiresAt = DateTime.UtcNow.AddHours(expiresInHours);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim("tenant_id", user.TenantId.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Role, user.Role.ToString())
        };

        var token = new JwtSecurityToken(
            issuer: "BILayer",
            audience: "BILayer",
            claims: claims,
            expires: expiresAt,
            signingCredentials: creds);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}
