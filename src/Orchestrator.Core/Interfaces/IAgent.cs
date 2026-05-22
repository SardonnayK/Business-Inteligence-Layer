using Orchestrator.Core.Entities;
using Orchestrator.Core.Enums;

namespace Orchestrator.Core.Interfaces;

public interface IAgent
{
    AgentCapability Capability { get; }
    IReadOnlyList<string> RequiredContext { get; }
    string? InputSchema { get; }
    Task<AgentResult> ExecuteAsync(AgentRequest request, CancellationToken ct);
}

public record AgentRequest(
    Guid ExecutionId,
    Guid TenantId,
    AgentCapability RequestType,
    string? TextInput,
    byte[]? FileBytes,
    string? FileName,
    string? MimeType,
    Guid? HintArtifactId,
    AgentContext Context);

public record AgentContext(
    IReadOnlyList<Artifact> Artifacts,
    IReadOnlyList<Department> Departments,
    string? DepartmentManifest,
    IReadOnlyList<BusinessContext> RagResults);

public record AgentResult(
    bool Success,
    string? Output,
    Guid? RoutedArtifactId,
    bool RequiresUserConfirmation,
    string? ConfirmationMessage,
    Guid? SuggestedArtifactId,
    IReadOnlyList<IngestItem> IngestItems);

public record IngestItem(
    string Text,
    string? Source,
    string? Category,
    Guid? ArtifactId);
