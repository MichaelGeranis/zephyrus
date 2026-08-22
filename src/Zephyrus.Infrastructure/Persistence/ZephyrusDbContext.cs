using Microsoft.EntityFrameworkCore;
using Zephyrus.Core.Entities;
using Zephyrus.Core.Interfaces;

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
    public DbSet<AgentInvocation> AgentInvocations => Set<AgentInvocation>();

    private readonly ISecretProtector _secretProtector;

    public ZephyrusDbContext(DbContextOptions<ZephyrusDbContext> options, ISecretProtector secretProtector)
        : base(options)
    {
        _secretProtector = secretProtector;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ZephyrusDbContext).Assembly);

        // The code-host token is encrypted on the way to the database and
        // decrypted on the way back, so entities always carry the plaintext and
        // no caller has to know the column is protected.
        modelBuilder.Entity<Project>()
            .Property(p => p.GitHubToken)
            .HasConversion(
                token => _secretProtector.Protect(token),
                stored => _secretProtector.Unprotect(stored));
    }
}
