using Orchestrator.Core.Entities;

namespace Orchestrator.Core.Interfaces;

public record McpToolInfo(string Name, string Description, string InputSchemaJson);

public interface IMcpClientService
{
    Task<IReadOnlyList<McpToolInfo>> ListToolsAsync(McpIntegration integration, CancellationToken ct);
    Task<string> CallToolAsync(McpIntegration integration, string toolName,
        IDictionary<string, object?> arguments, CancellationToken ct);
}
