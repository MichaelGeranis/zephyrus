using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zephyrus.Core.Entities;
using Zephyrus.Core.Enums;

namespace Zephyrus.Infrastructure.Persistence.Configurations;

public class TaskItemConfiguration : IEntityTypeConfiguration<TaskItem>
{
    public void Configure(EntityTypeBuilder<TaskItem> builder)
    {
        builder.ToTable("task_items");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id)
            .HasColumnName("id");

        builder.Property(t => t.FeatureId)
            .HasColumnName("feature_id")
            .IsRequired();

        builder.Property(t => t.ExternalIssueId)
            .HasColumnName("external_issue_id");

        builder.Property(t => t.Title)
            .HasColumnName("title")
            .HasMaxLength(512)
            .IsRequired();

        builder.Property(t => t.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(t => t.PrId)
            .HasColumnName("pr_id");

        builder.Property(t => t.AgentType)
            .HasColumnName("agent_type")
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();
    }
}
