namespace Orchestrator.Core.Entities;

public class Requirement
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string Content { get; set; } = string.Empty;
    public string Status { get; set; } = "Draft";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Project Project { get; set; } = null!;
}
