using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using ModelContextProtocol.Server;
using Orchestrator.Api.Auth;
using Orchestrator.Api.Mcp;
using Orchestrator.Api.Middleware;
using Orchestrator.Core.Entities;
using Orchestrator.Core.Enums;
using Orchestrator.Infrastructure;
using Orchestrator.Infrastructure.Data;
using Orchestrator.Infrastructure.Seeding;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddCors(options =>
{
    options.AddPolicy("dashboard", policy =>
    {
        policy.WithOrigins("http://localhost:5173", "http://localhost:3000")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var jwtSecret = builder.Configuration["Jwt:Secret"]
    ?? "dev-secret-key-change-in-production-must-be-32-chars!";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = "BILayer",
            ValidateAudience = true,
            ValidAudience = "BILayer",
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddScoped<JwtTokenGenerator>();

builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.AddNpgsqlDbContext<AppDbContext>("orchestrator",
    configureDbContextOptions: options => options.UseNpgsql(o => o.UseVector()));

builder.Services.AddInfrastructureServices(builder.Configuration);

builder.Services.AddMcpServer()
    .WithHttpTransport()
    .WithTools<BiLayerMcpTools>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();

    // If Aspire injected a Docker Model Runner endpoint and no system default exists yet, seed one.
    // Aspire injects env vars with __ separators; .NET config normalises them to : so read with colon.
    var modelRunnerEndpoint = app.Configuration["ModelRunner:Endpoint"];
    if (modelRunnerEndpoint is not null)
    {
        var hasSystemConfig = await db.EmbeddingProviderConfigs.AnyAsync(c => c.TenantId == null);
        if (!hasSystemConfig)
        {
            db.EmbeddingProviderConfigs.Add(new EmbeddingProviderConfig
            {
                Id = Guid.NewGuid(),
                TenantId = null,
                ProviderType = EmbeddingProviderType.DockerModelRunner,
                ModelId = "ai/nomic-embed-text-v1.5",
                ChatModelId = "ai/llama3.2",
                Endpoint = modelRunnerEndpoint,
                UpdatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }
    }

    if (!await db.AgentRegistrations.AnyAsync(ar => ar.TenantId == null))
    {
        db.AgentRegistrations.AddRange(
            new Orchestrator.Core.Entities.AgentRegistration
            {
                Id = Guid.NewGuid(),
                AgentType = Orchestrator.Core.Enums.AgentType.BuiltIn,
                Capability = Orchestrator.Core.Enums.AgentCapability.Ingest,
                Name = "Ingestion Agent",
                Priority = 10,
                IsEnabled = true
            },
            new Orchestrator.Core.Entities.AgentRegistration
            {
                Id = Guid.NewGuid(),
                AgentType = Orchestrator.Core.Enums.AgentType.BuiltIn,
                Capability = Orchestrator.Core.Enums.AgentCapability.Discover,
                Name = "Supervisor Agent",
                Priority = 10,
                IsEnabled = true
            },
            new Orchestrator.Core.Entities.AgentRegistration
            {
                Id = Guid.NewGuid(),
                AgentType = Orchestrator.Core.Enums.AgentType.BuiltIn,
                Capability = Orchestrator.Core.Enums.AgentCapability.Query,
                Name = "Supervisor Agent",
                Priority = 10,
                IsEnabled = true
            },
            new Orchestrator.Core.Entities.AgentRegistration
            {
                Id = Guid.NewGuid(),
                AgentType = Orchestrator.Core.Enums.AgentType.BuiltIn,
                Capability = Orchestrator.Core.Enums.AgentCapability.General,
                Name = "General Agent",
                Priority = 5,
                IsEnabled = true
            }
        );
        await db.SaveChangesAsync();
    }
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();

    app.MapPost("/api/dev/seed", async (DataSeeder seeder, CancellationToken ct) =>
    {
        var result = await seeder.SeedAsync(ct);
        return Results.Ok(result);
    });
}

app.UseCors("dashboard");
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<GuardrailMiddleware>();
app.MapControllers();
app.MapDefaultEndpoints();

// MCP server — protected by API key (X-Api-Key header)
var mcpApiKeys = app.Configuration.GetSection("Mcp:ApiKeys").Get<string[]>() ?? [];
app.MapMcp("/mcp").AddEndpointFilter(async (ctx, next) =>
{
    if (mcpApiKeys.Length > 0)
    {
        if (!ctx.HttpContext.Request.Headers.TryGetValue("X-Api-Key", out var key)
            || !mcpApiKeys.Contains(key.ToString()))
        {
            ctx.HttpContext.Response.StatusCode = 401;
            await ctx.HttpContext.Response.WriteAsync("Unauthorized: valid X-Api-Key required.");
            return null;
        }
    }
    return await next(ctx);
});

app.Run();
