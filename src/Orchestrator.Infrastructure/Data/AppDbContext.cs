using Microsoft.EntityFrameworkCore;
using Orchestrator.Core.Entities;

namespace Orchestrator.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<Requirement> Requirements => Set<Requirement>();
    public DbSet<BusinessContext> BusinessContexts => Set<BusinessContext>();
    public DbSet<EmbeddingProviderConfig> EmbeddingProviderConfigs => Set<EmbeddingProviderConfig>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Artifact> Artifacts => Set<Artifact>();
    public DbSet<TenantUser> TenantUsers => Set<TenantUser>();
    public DbSet<ArtifactPermission> ArtifactPermissions => Set<ArtifactPermission>();
    public DbSet<ArtifactDepartment> ArtifactDepartments => Set<ArtifactDepartment>();
    public DbSet<DepartmentManifest> DepartmentManifests => Set<DepartmentManifest>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("vector");

        modelBuilder.Entity<Tenant>(b =>
        {
            b.HasKey(t => t.Id);
            b.HasMany(t => t.Projects).WithOne(p => p.Tenant).HasForeignKey(p => p.TenantId);
            b.HasMany(t => t.BusinessContexts).WithOne(bc => bc.Tenant).HasForeignKey(bc => bc.TenantId);
            b.HasMany(t => t.Departments).WithOne(d => d.Tenant).HasForeignKey(d => d.TenantId);
            b.HasMany(t => t.Artifacts).WithOne(a => a.Tenant).HasForeignKey(a => a.TenantId);
            b.HasMany(t => t.Users).WithOne(u => u.Tenant).HasForeignKey(u => u.TenantId);
        });

        modelBuilder.Entity<Project>(b =>
        {
            b.HasKey(p => p.Id);
            b.HasMany(p => p.Requirements).WithOne(r => r.Project).HasForeignKey(r => r.ProjectId);
        });

        modelBuilder.Entity<Requirement>(b =>
        {
            b.HasKey(r => r.Id);
        });

        modelBuilder.Entity<BusinessContext>(b =>
        {
            b.HasKey(bc => bc.Id);
            b.Property(bc => bc.Embedding).HasColumnType("vector");
            b.HasIndex(bc => bc.TenantId);
            b.HasIndex(bc => bc.ArtifactId);
            b.Property(bc => bc.ArtifactId).IsRequired(false);
        });

        modelBuilder.Entity<EmbeddingProviderConfig>(b =>
        {
            b.HasKey(e => e.Id);
            b.Property(e => e.TenantId).IsRequired(false);
            b.Property(e => e.ProviderType).HasConversion<int>();
            b.Property(e => e.ModelId).IsRequired();
            b.Property(e => e.ApiKey).IsRequired(false);
            b.Property(e => e.Endpoint).IsRequired(false);
            b.Property(e => e.ChatModelId).IsRequired(false);
        });

        modelBuilder.Entity<Department>(b =>
        {
            b.HasKey(d => d.Id);
            b.HasIndex(d => d.TenantId);
        });

        modelBuilder.Entity<Artifact>(b =>
        {
            b.HasKey(a => a.Id);
            b.HasMany(a => a.BusinessContexts).WithOne(bc => bc.Artifact).HasForeignKey(bc => bc.ArtifactId).IsRequired(false);
            b.HasMany(a => a.Permissions).WithOne(ap => ap.Artifact).HasForeignKey(ap => ap.ArtifactId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(a => a.TenantId);
        });

        modelBuilder.Entity<ArtifactDepartment>(b =>
        {
            b.HasKey(ad => new { ad.ArtifactId, ad.DepartmentId });
            b.HasOne(ad => ad.Artifact).WithMany(a => a.ArtifactDepartments).HasForeignKey(ad => ad.ArtifactId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(ad => ad.Department).WithMany(d => d.ArtifactDepartments).HasForeignKey(ad => ad.DepartmentId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DepartmentManifest>(b =>
        {
            b.HasKey(dm => dm.Id);
            b.HasOne(dm => dm.Tenant).WithOne(t => t.DepartmentManifest).HasForeignKey<DepartmentManifest>(dm => dm.TenantId);
            b.HasIndex(dm => dm.TenantId).IsUnique();
        });

        modelBuilder.Entity<TenantUser>(b =>
        {
            b.HasKey(u => u.Id);
            b.Property(u => u.Role).HasConversion<int>();
            b.HasIndex(u => u.TenantId);
            b.HasIndex(u => new { u.TenantId, u.Username }).IsUnique();
            b.HasMany(u => u.ArtifactPermissions).WithOne(ap => ap.User)
                .HasForeignKey(ap => ap.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ArtifactPermission>(b =>
        {
            b.HasKey(ap => ap.Id);
            b.HasIndex(ap => ap.UserId);
            b.HasIndex(ap => ap.ArtifactId);
            b.HasIndex(ap => new { ap.UserId, ap.ArtifactId }).IsUnique();
        });
    }
}
