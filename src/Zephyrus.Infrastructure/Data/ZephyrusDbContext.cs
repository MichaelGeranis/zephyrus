using Microsoft.EntityFrameworkCore;
using Zephyrus.Core.Entities;

namespace Zephyrus.Infrastructure.Data;

public class ZephyrusDbContext : DbContext
{
    public ZephyrusDbContext(DbContextOptions<ZephyrusDbContext> options) : base(options)
    {
    }

    public DbSet<Project> Projects { get; set; }
    public DbSet<Feature> Features { get; set; }
    public DbSet<Artifact> Artifacts { get; set; }
    public DbSet<PipelineStep> PipelineSteps { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Project configuration
        modelBuilder.Entity<Project>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Name).IsRequired().HasMaxLength(100);
            entity.Property(p => p.Description).HasMaxLength(500);
            entity.Property(p => p.GitHubRepositoryUrl).HasMaxLength(500);
            entity.Property(p => p.CreatedAt).IsRequired();
            entity.Property(p => p.UpdatedAt).IsRequired();
        });

        // Feature configuration with CASCADE delete
        modelBuilder.Entity<Feature>(entity =>
        {
            entity.HasKey(f => f.Id);
            entity.Property(f => f.Title).IsRequired().HasMaxLength(200);
            entity.Property(f => f.Description).HasMaxLength(1000);
            entity.Property(f => f.CreatedAt).IsRequired();
            entity.Property(f => f.UpdatedAt).IsRequired();
            
            entity.HasOne(f => f.Project)
                  .WithMany(p => p.Features)
                  .HasForeignKey(f => f.ProjectId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Artifact configuration with CASCADE delete
        modelBuilder.Entity<Artifact>(entity =>
        {
            entity.HasKey(a => a.Id);
            entity.Property(a => a.Name).IsRequired().HasMaxLength(200);
            entity.Property(a => a.Type).IsRequired();
            entity.Property(a => a.Content).IsRequired();
            entity.Property(a => a.CreatedAt).IsRequired();
            entity.Property(a => a.UpdatedAt).IsRequired();
            
            entity.HasOne(a => a.Feature)
                  .WithMany(f => f.Artifacts)
                  .HasForeignKey(a => a.FeatureId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // PipelineStep configuration with CASCADE delete
        modelBuilder.Entity<PipelineStep>(entity =>
        {
            entity.HasKey(ps => ps.Id);
            entity.Property(ps => ps.State).IsRequired();
            entity.Property(ps => ps.Agent).IsRequired();
            entity.Property(ps => ps.CreatedAt).IsRequired();
            entity.Property(ps => ps.UpdatedAt).IsRequired();
            
            entity.HasOne(ps => ps.Project)
                  .WithMany(p => p.PipelineSteps)
                  .HasForeignKey(ps => ps.ProjectId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }
}