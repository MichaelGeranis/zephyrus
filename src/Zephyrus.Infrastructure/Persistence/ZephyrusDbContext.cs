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
    public DbSet<AgentJobRecord> AgentJobs => Set<AgentJobRecord>();
    public DbSet<ModelChoiceChange> ModelChoiceChanges => Set<ModelChoiceChange>();

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

        // An agent run's input and output hold the project's constitution, its
        // artifacts, issue bodies and code: encrypted the same way. Rows stored
        // before this are encrypted by AgentRunContentMaintenance.
        modelBuilder.Entity<AgentInvocation>()
            .Property(i => i.UserMessage)
            .HasConversion(
                text => _secretProtector.Protect(text),
                stored => _secretProtector.Unprotect(stored));
        modelBuilder.Entity<AgentInvocation>()
            .Property(i => i.Response)
            .HasConversion(
                text => _secretProtector.Protect(text),
                stored => _secretProtector.Unprotect(stored));
    }
}
