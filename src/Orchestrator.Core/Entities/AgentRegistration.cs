using Orchestrator.Core.Enums;

namespace Orchestrator.Core.Entities;

public class AgentRegistration
{
    public Guid Id { get; set; }
    public Guid? TenantId { get; set; }
    public AgentType AgentType { get; set; }
    public AgentCapability Capability { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? HttpEndpoint { get; set; }
    public string? InputSchemaJson { get; set; }
    public bool IsEnabled { get; set; } = true;
    public int Priority { get; set; } = 0;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public Tenant? Tenant { get; set; }
}
