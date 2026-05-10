using Pgvector;

namespace Orchestrator.Core.Entities;

public class BusinessContext
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Text { get; set; } = string.Empty;
    public Vector Embedding { get; set; } = null!;
    public string? Source { get; set; }
    public string? Category { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Guid? ArtifactId { get; set; }

    public Tenant Tenant { get; set; } = null!;
    public Artifact? Artifact { get; set; }
}
