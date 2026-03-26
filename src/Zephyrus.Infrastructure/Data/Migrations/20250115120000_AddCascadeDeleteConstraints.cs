using Microsoft.EntityFrameworkCore.Migrations;

namespace Zephyrus.Infrastructure.Data.Migrations;

/// <inheritdoc />
public partial class AddCascadeDeleteConstraints : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Drop existing foreign key constraints
        migrationBuilder.DropForeignKey(
            name: "FK_Features_Projects_ProjectId",
            table: "Features");

        migrationBuilder.DropForeignKey(
            name: "FK_Artifacts_Features_FeatureId",
            table: "Artifacts");

        migrationBuilder.DropForeignKey(
            name: "FK_PipelineSteps_Projects_ProjectId",
            table: "PipelineSteps");

        // Add foreign key constraints with CASCADE delete
        migrationBuilder.AddForeignKey(
            name: "FK_Features_Projects_ProjectId",
            table: "Features",
            column: "ProjectId",
            principalTable: "Projects",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);

        migrationBuilder.AddForeignKey(
            name: "FK_Artifacts_Features_FeatureId",
            table: "Artifacts",
            column: "FeatureId",
            principalTable: "Features",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);

        migrationBuilder.AddForeignKey(
            name: "FK_PipelineSteps_Projects_ProjectId",
            table: "PipelineSteps",
            column: "ProjectId",
            principalTable: "Projects",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Drop CASCADE foreign key constraints
        migrationBuilder.DropForeignKey(
            name: "FK_Features_Projects_ProjectId",
            table: "Features");

        migrationBuilder.DropForeignKey(
            name: "FK_Artifacts_Features_FeatureId",
            table: "Artifacts");

        migrationBuilder.DropForeignKey(
            name: "FK_PipelineSteps_Projects_ProjectId",
            table: "PipelineSteps");

        // Add original foreign key constraints without CASCADE
        migrationBuilder.AddForeignKey(
            name: "FK_Features_Projects_ProjectId",
            table: "Features",
            column: "ProjectId",
            principalTable: "Projects",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "FK_Artifacts_Features_FeatureId",
            table: "Artifacts",
            column: "FeatureId",
            principalTable: "Features",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "FK_PipelineSteps_Projects_ProjectId",
            table: "PipelineSteps",
            column: "ProjectId",
            principalTable: "Projects",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }
}