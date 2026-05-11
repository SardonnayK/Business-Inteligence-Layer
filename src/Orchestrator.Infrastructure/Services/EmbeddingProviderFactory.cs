using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.AI;
using OpenAI;
using Orchestrator.Core.Entities;
using Orchestrator.Core.Enums;
using Orchestrator.Core.Interfaces;

namespace Orchestrator.Infrastructure.Services;

public sealed class EmbeddingProviderFactory : IEmbeddingProviderFactory
{
    private readonly ConcurrentDictionary<string, IEmbeddingGenerator<string, Embedding<float>>> _cache = new();

    public IEmbeddingGenerator<string, Embedding<float>> GetGenerator(EmbeddingProviderConfig config)
    {
        var cacheKey = BuildCacheKey(config);
        return _cache.GetOrAdd(cacheKey, _ => CreateGenerator(config));
    }

    private static IEmbeddingGenerator<string, Embedding<float>> CreateGenerator(EmbeddingProviderConfig config)
    {
        return config.ProviderType switch
        {
            EmbeddingProviderType.OpenAI => CreateOpenAIGenerator(config),
            EmbeddingProviderType.AzureOpenAI => CreateAzureOpenAIGenerator(config),
            EmbeddingProviderType.Ollama => CreateOllamaGenerator(config),
            EmbeddingProviderType.DockerModelRunner => CreateDockerModelRunnerGenerator(config),
            EmbeddingProviderType.None => throw new InvalidOperationException(
                "No embedding provider configured for this tenant. " +
                "Configure one at /api/embedding-config/system or /api/embedding-config/tenant/{id}."),
            _ => throw new InvalidOperationException(
                $"Unsupported embedding provider type: {config.ProviderType}.")
        };
    }

    private static IEmbeddingGenerator<string, Embedding<float>> CreateOpenAIGenerator(EmbeddingProviderConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.ApiKey))
            throw new InvalidOperationException("OpenAI provider requires an API key.");

        var options = new OpenAIClientOptions();
        if (!string.IsNullOrWhiteSpace(config.Endpoint))
            options.Endpoint = new Uri(config.Endpoint);

        var client = new OpenAIClient(new System.ClientModel.ApiKeyCredential(config.ApiKey), options);
        return client.GetEmbeddingClient(config.ModelId).AsIEmbeddingGenerator();
    }

    private static IEmbeddingGenerator<string, Embedding<float>> CreateAzureOpenAIGenerator(EmbeddingProviderConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.ApiKey))
            throw new InvalidOperationException("Azure OpenAI provider requires an API key.");
        if (string.IsNullOrWhiteSpace(config.Endpoint))
            throw new InvalidOperationException("Azure OpenAI provider requires an endpoint URL.");

        var options = new OpenAIClientOptions
        {
            Endpoint = new Uri(config.Endpoint)
        };

        var client = new OpenAIClient(new System.ClientModel.ApiKeyCredential(config.ApiKey), options);
        return client.GetEmbeddingClient(config.ModelId).AsIEmbeddingGenerator();
    }

    private static IEmbeddingGenerator<string, Embedding<float>> CreateOllamaGenerator(EmbeddingProviderConfig config)
    {
        var endpoint = string.IsNullOrWhiteSpace(config.Endpoint)
            ? "http://localhost:11434"
            : config.Endpoint;

        // OllamaApiClient in v5 implements IEmbeddingGenerator<string, Embedding<float>> explicitly.
        var ollamaClient = new OllamaSharp.OllamaApiClient(new Uri(endpoint), config.ModelId);
        return (IEmbeddingGenerator<string, Embedding<float>>)ollamaClient;
    }

    private static IEmbeddingGenerator<string, Embedding<float>> CreateDockerModelRunnerGenerator(EmbeddingProviderConfig config)
    {
        var host = string.IsNullOrWhiteSpace(config.Endpoint)
            ? "http://localhost:12434"
            : config.Endpoint.TrimEnd('/');

        var options = new OpenAIClientOptions { Endpoint = new Uri($"{host}/engines/v1") };
        var client = new OpenAIClient(new System.ClientModel.ApiKeyCredential("docker"), options);
        return client.GetEmbeddingClient(config.ModelId).AsIEmbeddingGenerator();
    }

    private static string BuildCacheKey(EmbeddingProviderConfig config)
    {
        var apiKeyHash = string.IsNullOrWhiteSpace(config.ApiKey)
            ? string.Empty
            : Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(config.ApiKey)));

        return $"{config.ProviderType}|{config.ModelId}|{config.Endpoint ?? string.Empty}|{apiKeyHash}";
    }
}
