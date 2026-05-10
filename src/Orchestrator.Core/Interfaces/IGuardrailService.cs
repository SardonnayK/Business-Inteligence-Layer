namespace Orchestrator.Core.Interfaces;

public record GuardrailResult(bool IsValid, string? Reason = null);

public interface IGuardrailService
{
    Task<GuardrailResult> ValidateAsync(string aiOutput, Guid tenantId, CancellationToken ct = default);
}
