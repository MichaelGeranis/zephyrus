using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zephyrus.Core.Entities;

namespace Zephyrus.Infrastructure.Persistence.Configurations;

public class AgentInvocationConfiguration : IEntityTypeConfiguration<AgentInvocation>
{
    public void Configure(EntityTypeBuilder<AgentInvocation> builder)
    {
        builder.ToTable("agent_invocations");

        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id)
            .HasColumnName("id");

        builder.Property(i => i.FeatureId)
            .HasColumnName("feature_id")
            .IsRequired();

        builder.Property(i => i.AgentName)
            .HasColumnName("agent_name")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(i => i.SystemPrompt)
            .HasColumnName("system_prompt")
            .IsRequired();

        builder.Property(i => i.UserMessage)
            .HasColumnName("user_message")
            .IsRequired();

        builder.Property(i => i.Response)
            .HasColumnName("response")
            .IsRequired();

        builder.Property(i => i.InvokedAt)
            .HasColumnName("invoked_at")
            .IsRequired();

        builder.Property(i => i.DurationMs)
            .HasColumnName("duration_ms")
            .IsRequired();

        builder.Property(i => i.Model).HasColumnName("model").HasMaxLength(200);
        builder.Property(i => i.InputTokens).HasColumnName("input_tokens");
        builder.Property(i => i.OutputTokens).HasColumnName("output_tokens");
        builder.Property(i => i.EstimatedCostUsd)
            .HasColumnName("estimated_cost_usd")
            .HasPrecision(12, 6);

        builder.Property(i => i.PromptVersion)
            .HasColumnName("prompt_version")
            .HasMaxLength(12)
            .IsRequired();

        // No foreign key: a re-run deletes the artifact it replaces, and the
        // record of which run produced it must outlive it.
        builder.Property(i => i.ArtifactId).HasColumnName("artifact_id");

        builder.HasIndex(i => i.FeatureId);
    }
}
