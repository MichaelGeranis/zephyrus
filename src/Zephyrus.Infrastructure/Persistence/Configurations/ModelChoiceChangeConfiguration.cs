using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zephyrus.Core.Entities;

namespace Zephyrus.Infrastructure.Persistence.Configurations;

public class ModelChoiceChangeConfiguration : IEntityTypeConfiguration<ModelChoiceChange>
{
    public void Configure(EntityTypeBuilder<ModelChoiceChange> builder)
    {
        builder.ToTable("model_choice_changes");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("id");
        builder.Property(c => c.ProjectId).HasColumnName("project_id").IsRequired();
        builder.Property(c => c.Stage)
            .HasColumnName("stage")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();
        builder.Property(c => c.Primary).HasColumnName("primary_model").HasMaxLength(200);
        builder.Property(c => c.Fallback).HasColumnName("fallback_model").HasMaxLength(200);
        builder.Property(c => c.ChangedBy).HasColumnName("changed_by").HasMaxLength(256).IsRequired();
        builder.Property(c => c.ChangedAt).HasColumnName("changed_at").IsRequired();
        builder.Ignore(c => c.IsReset);

        builder.HasOne<Project>()
            .WithMany()
            .HasForeignKey(c => c.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(c => new { c.ProjectId, c.ChangedAt });
    }
}
