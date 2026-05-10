using Microsoft.Extensions.AI;
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
        var openAiKey = configuration["OpenAI:ApiKey"];

        if (string.IsNullOrWhiteSpace(openAiKey))
            services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>, UnconfiguredEmbeddingGenerator>();
        else
            services.AddOpenAIEmbeddingGenerator("text-embedding-ada-002", openAiKey);

        services.AddScoped<IHybridRagService, HybridRagService>();
        services.AddScoped<IGuardrailService, GuardrailService>();
        services.AddScoped<DataSeeder>();

        return services;
    }
}

/// <summary>
/// Placeholder generator used when OpenAI:ApiKey is not configured.
/// Lets the app start so non-embedding routes work; throws on actual embedding calls.
/// </summary>
file sealed class UnconfiguredEmbeddingGenerator : IEmbeddingGenerator<string, Embedding<float>>
{
    public EmbeddingGeneratorMetadata Metadata { get; } = new EmbeddingGeneratorMetadata();

    public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        IEnumerable<string> values,
        EmbeddingGenerationOptions? options = null,
        CancellationToken cancellationToken = default)
        => throw new InvalidOperationException(
            "OpenAI:ApiKey is not configured. Set it via user secrets: " +
            "dotnet user-secrets set \"OpenAI:ApiKey\" \"sk-...\" --project src/Orchestrator.AppHost");

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose() { }
}
