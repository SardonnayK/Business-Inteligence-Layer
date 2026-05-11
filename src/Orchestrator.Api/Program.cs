using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Orchestrator.Api.Auth;
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

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();

    // If Aspire injected a Docker Model Runner endpoint and no system default exists yet, seed one.
    var modelRunnerEndpoint = app.Configuration["ModelRunner__Endpoint"];
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
                ChatModelId = "ai/phi4-mini",
                Endpoint = modelRunnerEndpoint,
                UpdatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }
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
app.Run();
