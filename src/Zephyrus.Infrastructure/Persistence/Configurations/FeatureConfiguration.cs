using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zephyrus.Core.Entities;
using Zephyrus.Core.Enums;

namespace Zephyrus.Infrastructure.Persistence.Configurations;

public class FeatureConfiguration : IEntityTypeConfiguration<Feature>
{
    public void Configure(EntityTypeBuilder<Feature> builder)
    {
        builder.ToTable("features");

        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id)
            .HasColumnName("id");

        builder.Property(f => f.ProjectId)
            .HasColumnName("project_id")
            .IsRequired();

        builder.Property(f => f.Prompt)
            .HasColumnName("prompt")
            .IsRequired();

        builder.Property(f => f.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(f => f.CreatedAt)
            .HasColumnName("created_at");

        builder.HasMany(f => f.Artifacts)
            .WithOne(a => a.Feature)
            .HasForeignKey(a => a.FeatureId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(f => f.Tasks)
            .WithOne(t => t.Feature)
            .HasForeignKey(t => t.FeatureId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(f => f.PipelineEvents)
            .WithOne(pe => pe.Feature)
            .HasForeignKey(pe => pe.FeatureId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(f => f.Deployments)
            .WithOne(d => d.Feature)
            .HasForeignKey(d => d.FeatureId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
