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

        builder.HasIndex(i => i.FeatureId);
    }
}
