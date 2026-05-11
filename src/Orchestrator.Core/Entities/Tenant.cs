namespace Orchestrator.Core.Entities;

public class Tenant
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Project> Projects { get; set; } = new List<Project>();
    public ICollection<BusinessContext> BusinessContexts { get; set; } = new List<BusinessContext>();
    public ICollection<Department> Departments { get; set; } = new List<Department>();
    public ICollection<Artifact> Artifacts { get; set; } = new List<Artifact>();
    public ICollection<TenantUser> Users { get; set; } = new List<TenantUser>();
    public DepartmentManifest? DepartmentManifest { get; set; }
}
