using Microsoft.EntityFrameworkCore.Migrations;

namespace Zephyrus.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class ConfigureCascadeDeleteRelationships : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // The cascade delete relationships are already configured in the Entity Framework configurations
        // (ProjectConfiguration and FeatureConfiguration), so this migration serves as a record
        // that the cascade delete configuration has been explicitly reviewed and applied.
        
        // Project → Feature cascade delete is configured in ProjectConfiguration
        // Feature → Artifact cascade delete is configured in FeatureConfiguration
        // Feature → TaskItem cascade delete is configured in FeatureConfiguration
        // Feature → PipelineEvent cascade delete is configured in FeatureConfiguration
        // Feature → Deployment cascade delete is configured in FeatureConfiguration
        
        // No schema changes are needed as the relationships were already properly configured
        // with CASCADE DELETE behavior in the existing configurations.
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // No changes to revert as cascade delete configuration is handled by Entity Framework configurations
    }
}