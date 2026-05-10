using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Orchestrator.Api.Middleware;
using Orchestrator.Infrastructure;
using Orchestrator.Infrastructure.Data;
using Orchestrator.Infrastructure.Seeding;

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
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    // Seed both competitor tenants with relational data + embedded business context.
    // Idempotent — safe to call multiple times. Requires a valid OpenAI:ApiKey.
    app.MapPost("/api/dev/seed", async (DataSeeder seeder, CancellationToken ct) =>
    {
        var result = await seeder.SeedAsync(ct);
        return Results.Ok(result);
    });
}

app.UseCors("dashboard");
app.UseMiddleware<GuardrailMiddleware>();
app.MapControllers();
app.MapDefaultEndpoints();
app.Run();
