using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zephyrus.Core.Entities;
using Zephyrus.Core.Enums;

namespace Zephyrus.Infrastructure.Persistence.Configurations;

public class ArtifactConfiguration : IEntityTypeConfiguration<Artifact>
{
    public void Configure(EntityTypeBuilder<Artifact> builder)
    {
        builder.ToTable("artifacts");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id)
            .HasColumnName("id");

        builder.Property(a => a.FeatureId)
            .HasColumnName("feature_id")
            .IsRequired();

        builder.Property(a => a.Type)
            .HasColumnName("type")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(a => a.RepositoryPath)
            .HasColumnName("repository_path")
            .HasMaxLength(1024)
            .IsRequired();

        builder.Property(a => a.ApprovedBy)
            .HasColumnName("approved_by")
            .HasMaxLength(256);

        builder.Property(a => a.ApprovedAt)
            .HasColumnName("approved_at");

        builder.Property(a => a.CommitSucceeded)
            .HasColumnName("commit_succeeded")
            .HasDefaultValue(false);

        builder.Property(a => a.PendingContent)
            .HasColumnName("pending_content")
            .HasColumnType("text");
    }
}
