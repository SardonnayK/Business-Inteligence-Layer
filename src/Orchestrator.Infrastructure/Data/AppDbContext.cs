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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("vector");

        modelBuilder.Entity<Tenant>(b =>
        {
            b.HasKey(t => t.Id);
            b.HasMany(t => t.Projects).WithOne(p => p.Tenant).HasForeignKey(p => p.TenantId);
            b.HasMany(t => t.BusinessContexts).WithOne(bc => bc.Tenant).HasForeignKey(bc => bc.TenantId);
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
        });

        modelBuilder.Entity<EmbeddingProviderConfig>(b =>
        {
            b.HasKey(e => e.Id);
            b.Property(e => e.TenantId).IsRequired(false);
            b.Property(e => e.ProviderType).HasConversion<int>();
            b.Property(e => e.ModelId).IsRequired();
            b.Property(e => e.ApiKey).IsRequired(false);
            b.Property(e => e.Endpoint).IsRequired(false);
        });
    }
}
