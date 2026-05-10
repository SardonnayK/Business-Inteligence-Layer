using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Orchestrator.Core.Interfaces;
using Orchestrator.Infrastructure.Seeding;
using Orchestrator.Infrastructure.Services;

namespace Orchestrator.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registers infrastructure services. DbContext is registered separately via
    /// builder.AddNpgsqlDbContext (Aspire) or builder.Services.AddDbContext (non-Aspire).
    /// </summary>
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        var openAiKey = configuration["OpenAI:ApiKey"]
            ?? throw new InvalidOperationException("OpenAI:ApiKey is required");

        services.AddOpenAIEmbeddingGenerator("text-embedding-ada-002", openAiKey);

        services.AddScoped<IHybridRagService, HybridRagService>();
        services.AddScoped<IGuardrailService, GuardrailService>();
        services.AddScoped<DataSeeder>();

        return services;
    }
}
