using System.Text;
using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Orchestrator.Core.Entities;
using Orchestrator.Core.Interfaces;

namespace Orchestrator.Infrastructure.Services;

public class McpClientService : IMcpClientService
{
    private readonly IHttpClientFactory _httpClientFactory;

    public McpClientService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<IReadOnlyList<McpToolInfo>> ListToolsAsync(McpIntegration integration, CancellationToken ct)
    {
        await using var client = await CreateClientAsync(integration, ct);
        var tools = await client.ListToolsAsync(cancellationToken: ct);
        return tools.Select(t => new McpToolInfo(
            t.Name,
            t.Description ?? string.Empty,
            t.JsonSchema.ToString())).ToList();
    }

    public async Task<string> CallToolAsync(
        McpIntegration integration,
        string toolName,
        IDictionary<string, object?> arguments,
        CancellationToken ct)
    {
        await using var client = await CreateClientAsync(integration, ct);

        var typedArgs = arguments.ToDictionary(
            kvp => kvp.Key,
            kvp => (object?)ConvertToJsonElement(kvp.Value));

        var result = await client.CallToolAsync(
            toolName,
            typedArgs.AsReadOnly(),
            cancellationToken: ct);

        var sb = new StringBuilder();
        foreach (var block in result.Content)
        {
            if (block is TextContentBlock text)
                sb.AppendLine(text.Text);
        }

        return sb.ToString().Trim();
    }

    private async Task<McpClient> CreateClientAsync(McpIntegration integration, CancellationToken ct)
    {
        var httpClient = _httpClientFactory.CreateClient();
        httpClient.BaseAddress = new Uri(integration.ServerUrl);

        if (!string.IsNullOrEmpty(integration.ApiKey))
            httpClient.DefaultRequestHeaders.TryAddWithoutValidation(
                integration.AuthHeaderName, integration.ApiKey);

        var transport = new HttpClientTransport(
            new HttpClientTransportOptions { Endpoint = new Uri(integration.ServerUrl) },
            httpClient,
            loggerFactory: null,
            ownsHttpClient: true);

        return (McpClient)await McpClient.CreateAsync(transport, cancellationToken: ct);
    }

    private static object? ConvertToJsonElement(object? value)
    {
        if (value is JsonElement) return value;
        if (value is null) return null;
        var json = JsonSerializer.Serialize(value);
        return JsonSerializer.Deserialize<JsonElement>(json);
    }
}
