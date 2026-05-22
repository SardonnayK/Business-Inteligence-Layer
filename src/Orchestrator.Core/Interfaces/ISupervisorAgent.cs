using Orchestrator.Core.Enums;

namespace Orchestrator.Core.Interfaces;

public interface ISupervisorAgent
{
    Task<SupervisorResult> ProcessAsync(SupervisorRequest request, CancellationToken ct);
    Task<SupervisorResult> ConfirmAsync(Guid executionId, bool accept, CancellationToken ct);
}

public record SupervisorRequest(
    Guid TenantId,
    Guid UserId,
    UserRole UserRole,
    AgentCapability Capability,
    string? TextInput,
    byte[]? FileBytes,
    string? FileName,
    string? MimeType,
    Guid? HintArtifactId,
    string? Source = null,
    string? Category = null,
    Guid? ForceRegistrationId = null,
    bool SkipAutoIngest = false);

public record SupervisorResult(
    bool Success,
    string? Output,
    bool RequiresUserConfirmation,
    string? ConfirmationMessage,
    Guid? SuggestedArtifactId,
    Guid? RoutedArtifactId,
    IReadOnlyList<IngestItem> IngestedItems,
    string AgentName,
    Guid ExecutionId);
