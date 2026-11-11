using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zephyrus.Core.Entities;

namespace Zephyrus.Infrastructure.Persistence.Configurations;

public class AgentJobRecordConfiguration : IEntityTypeConfiguration<AgentJobRecord>
{
    public void Configure(EntityTypeBuilder<AgentJobRecord> builder)
    {
        builder.ToTable("agent_jobs");

        builder.HasKey(j => j.Id);
        builder.Property(j => j.Id).HasColumnName("id");

        builder.Property(j => j.FeatureId).HasColumnName("feature_id").IsRequired();

        builder.Property(j => j.Kind)
            .HasColumnName("kind")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(j => j.ForceRerun)
            .HasColumnName("force_rerun")
            .IsRequired();

        builder.Property(j => j.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(j => j.Attempts).HasColumnName("attempts").IsRequired();
        builder.Property(j => j.LastError).HasColumnName("last_error");
        builder.Property(j => j.EnqueuedAt).HasColumnName("enqueued_at").IsRequired();
        builder.Property(j => j.NextAttemptAt).HasColumnName("next_attempt_at").IsRequired();
        builder.Property(j => j.StartedAt).HasColumnName("started_at");
        builder.Property(j => j.LeaseExpiresAt).HasColumnName("lease_expires_at");
        builder.Property(j => j.CompletedAt).HasColumnName("completed_at");
        builder.Property(j => j.TraceParent).HasColumnName("trace_parent").HasMaxLength(128);

        // Two workers claiming the same row: the second save fails on this.
        builder.Property(j => j.ConcurrencyStamp)
            .HasColumnName("concurrency_stamp")
            .IsConcurrencyToken();

        // Deleting a feature removes its jobs.
        builder.HasOne<Feature>()
            .WithMany()
            .HasForeignKey(j => j.FeatureId)
            .OnDelete(DeleteBehavior.Cascade);

        // The worker's claim query filters on status and orders by age.
        builder.HasIndex(j => new { j.Status, j.EnqueuedAt });
        builder.HasIndex(j => j.FeatureId);
    }
}
