using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Orchestrator.Core.Enums;
using Orchestrator.Core.Interfaces;
using Orchestrator.Infrastructure.Agents;
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
        services.AddSingleton<IEmbeddingProviderFactory, EmbeddingProviderFactory>();
        services.AddSingleton<IChatProviderFactory, ChatProviderFactory>();
        services.AddScoped<IHybridRagService, HybridRagService>();
        services.AddScoped<IArtifactSupervisorService, ArtifactSupervisorService>();
        services.AddScoped<IGuardrailService, GuardrailService>();
        services.AddScoped<IPermissionService, PermissionService>();
        services.AddScoped<DataSeeder>();

        services.AddKeyedScoped<IAgent, IngestionAgent>(AgentCapability.Ingest);
        services.AddKeyedScoped<IAgent, GeneralAgent>(AgentCapability.General);
        services.AddScoped<ISupervisorAgent, SupervisorAgent>();
        services.AddSingleton<PendingConfirmationStore>();
        services.AddScoped<ITextExtractor, TextExtractor>();
        services.AddHttpClient();
        services.AddScoped<IMcpClientService, McpClientService>();

        return services;
    }
}
