using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.AI;
using OpenAI;
using Orchestrator.Core.Entities;
using Orchestrator.Core.Enums;
using Orchestrator.Core.Interfaces;

namespace Orchestrator.Infrastructure.Services;

public sealed class ChatProviderFactory : IChatProviderFactory
{
    private readonly ConcurrentDictionary<string, IChatClient> _cache = new();

    public IChatClient? GetChatClient(EmbeddingProviderConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.ChatModelId))
            return null;

        var cacheKey = BuildCacheKey(config);
        return _cache.GetOrAdd(cacheKey, _ => CreateChatClient(config));
    }

    private static IChatClient CreateChatClient(EmbeddingProviderConfig config)
    {
        return config.ProviderType switch
        {
            EmbeddingProviderType.OpenAI => CreateOpenAIChatClient(config),
            EmbeddingProviderType.AzureOpenAI => CreateAzureOpenAIChatClient(config),
            EmbeddingProviderType.Ollama => CreateOllamaChatClient(config),
            _ => throw new InvalidOperationException(
                $"Cannot create a chat client for provider type: {config.ProviderType}.")
        };
    }

    private static IChatClient CreateOpenAIChatClient(EmbeddingProviderConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.ApiKey))
            throw new InvalidOperationException("OpenAI chat provider requires an API key.");

        var options = new OpenAIClientOptions();
        if (!string.IsNullOrWhiteSpace(config.Endpoint))
            options.Endpoint = new Uri(config.Endpoint);

        var credential = new System.ClientModel.ApiKeyCredential(config.ApiKey);
        var client = new OpenAIClient(credential, options);
        return client.GetChatClient(config.ChatModelId!).AsIChatClient();
    }

    private static IChatClient CreateAzureOpenAIChatClient(EmbeddingProviderConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.ApiKey))
            throw new InvalidOperationException("Azure OpenAI chat provider requires an API key.");
        if (string.IsNullOrWhiteSpace(config.Endpoint))
            throw new InvalidOperationException("Azure OpenAI chat provider requires an endpoint URL.");

        var options = new OpenAIClientOptions
        {
            Endpoint = new Uri(config.Endpoint)
        };

        var credential = new System.ClientModel.ApiKeyCredential(config.ApiKey);
        var client = new OpenAIClient(credential, options);
        return client.GetChatClient(config.ChatModelId!).AsIChatClient();
    }

    private static IChatClient CreateOllamaChatClient(EmbeddingProviderConfig config)
    {
        var endpoint = string.IsNullOrWhiteSpace(config.Endpoint)
            ? "http://localhost:11434"
            : config.Endpoint;

        var ollamaClient = new OllamaSharp.OllamaApiClient(new Uri(endpoint), config.ChatModelId!);
        return (IChatClient)ollamaClient;
    }

    private static string BuildCacheKey(EmbeddingProviderConfig config)
    {
        var apiKeyHash = string.IsNullOrWhiteSpace(config.ApiKey)
            ? string.Empty
            : Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(config.ApiKey)));

        return $"{config.ProviderType}|{config.ChatModelId}|{config.Endpoint ?? string.Empty}|{apiKeyHash}";
    }
}
