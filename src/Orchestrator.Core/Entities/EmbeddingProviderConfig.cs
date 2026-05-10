using Orchestrator.Core.Enums;

namespace Orchestrator.Core.Entities;

public class EmbeddingProviderConfig
{
    public Guid Id { get; set; }
    public Guid? TenantId { get; set; }   // null = system default; non-null = tenant override
    public EmbeddingProviderType ProviderType { get; set; } = EmbeddingProviderType.None;
    public string ModelId { get; set; } = string.Empty;
    public string? ApiKey { get; set; }
    public string? Endpoint { get; set; } // Ollama: "http://localhost:11434"; Azure: deployment URL
    public string? ChatModelId { get; set; }  // generation model for supervisor, e.g. "gpt-4o-mini" or "llama3"
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
