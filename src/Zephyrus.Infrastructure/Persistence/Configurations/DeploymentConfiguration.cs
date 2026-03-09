using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zephyrus.Core.Entities;
using Zephyrus.Core.Enums;

namespace Zephyrus.Infrastructure.Persistence.Configurations;

public class DeploymentConfiguration : IEntityTypeConfiguration<Deployment>
{
    public void Configure(EntityTypeBuilder<Deployment> builder)
    {
        builder.ToTable("deployments");

        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id)
            .HasColumnName("id");

        builder.Property(d => d.FeatureId)
            .HasColumnName("feature_id")
            .IsRequired();

        builder.Property(d => d.Sha)
            .HasColumnName("sha")
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(d => d.Environment)
            .HasColumnName("environment")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(d => d.DeployedAt)
            .HasColumnName("deployed_at");

        builder.Property(d => d.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();
    }
}
