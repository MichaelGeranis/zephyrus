using Microsoft.EntityFrameworkCore;
using Zephyrus.Core.Entities;

namespace Zephyrus.Infrastructure.Persistence;

/// <summary>
/// Entity Framework Core DbContext for the Zephyrus database.
/// </summary>
public class ZephyrusDbContext : DbContext
{
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<Feature> Features => Set<Feature>();
    public DbSet<Artifact> Artifacts => Set<Artifact>();
    public DbSet<TaskItem> TaskItems => Set<TaskItem>();
    public DbSet<PipelineEvent> PipelineEvents => Set<PipelineEvent>();
    public DbSet<Deployment> Deployments => Set<Deployment>();

    public ZephyrusDbContext(DbContextOptions<ZephyrusDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ZephyrusDbContext).Assembly);
    }
}
