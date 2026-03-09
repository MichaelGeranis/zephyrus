using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zephyrus.Core.Entities;
using Zephyrus.Core.Enums;

namespace Zephyrus.Infrastructure.Persistence.Configurations;

public class PipelineEventConfiguration : IEntityTypeConfiguration<PipelineEvent>
{
    public void Configure(EntityTypeBuilder<PipelineEvent> builder)
    {
        builder.ToTable("pipeline_events");

        builder.HasKey(pe => pe.Id);
        builder.Property(pe => pe.Id)
            .HasColumnName("id");

        builder.Property(pe => pe.FeatureId)
            .HasColumnName("feature_id")
            .IsRequired();

        builder.Property(pe => pe.FromStatus)
            .HasColumnName("from_status")
            .HasConversion<string>()
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(pe => pe.ToStatus)
            .HasColumnName("to_status")
            .HasConversion<string>()
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(pe => pe.TriggeredBy)
            .HasColumnName("triggered_by")
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(pe => pe.Timestamp)
            .HasColumnName("timestamp");
    }
}
