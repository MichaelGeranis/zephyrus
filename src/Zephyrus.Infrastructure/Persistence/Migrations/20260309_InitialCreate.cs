using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable enable

namespace Zephyrus.Infrastructure.Persistence.Migrations;

/// <summary>
/// Initial database schema for Zephyrus — creates all core tables.
/// </summary>
public partial class InitialCreate : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "projects",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                description = table.Column<string>(type: "text", nullable: false),
                config = table.Column<string>(type: "jsonb", nullable: false),
                repository_slug = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_projects", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "features",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                project_id = table.Column<Guid>(type: "uuid", nullable: false),
                prompt = table.Column<string>(type: "text", nullable: false),
                status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_features", x => x.id);
                table.ForeignKey(
                    name: "FK_features_projects_project_id",
                    column: x => x.project_id,
                    principalTable: "projects",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "artifacts",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                feature_id = table.Column<Guid>(type: "uuid", nullable: false),
                type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                repository_path = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                approved_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                approved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_artifacts", x => x.id);
                table.ForeignKey(
                    name: "FK_artifacts_features_feature_id",
                    column: x => x.feature_id,
                    principalTable: "features",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "task_items",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                feature_id = table.Column<Guid>(type: "uuid", nullable: false),
                external_issue_id = table.Column<int>(type: "integer", nullable: true),
                title = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                pr_id = table.Column<int>(type: "integer", nullable: true),
                agent_type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_task_items", x => x.id);
                table.ForeignKey(
                    name: "FK_task_items_features_feature_id",
                    column: x => x.feature_id,
                    principalTable: "features",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "pipeline_events",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                feature_id = table.Column<Guid>(type: "uuid", nullable: false),
                from_status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                to_status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                triggered_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_pipeline_events", x => x.id);
                table.ForeignKey(
                    name: "FK_pipeline_events_features_feature_id",
                    column: x => x.feature_id,
                    principalTable: "features",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "deployments",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                feature_id = table.Column<Guid>(type: "uuid", nullable: false),
                sha = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                environment = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                deployed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_deployments", x => x.id);
                table.ForeignKey(
                    name: "FK_deployments_features_feature_id",
                    column: x => x.feature_id,
                    principalTable: "features",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_features_project_id",
            table: "features",
            column: "project_id");

        migrationBuilder.CreateIndex(
            name: "IX_artifacts_feature_id",
            table: "artifacts",
            column: "feature_id");

        migrationBuilder.CreateIndex(
            name: "IX_task_items_feature_id",
            table: "task_items",
            column: "feature_id");

        migrationBuilder.CreateIndex(
            name: "IX_pipeline_events_feature_id",
            table: "pipeline_events",
            column: "feature_id");

        migrationBuilder.CreateIndex(
            name: "IX_deployments_feature_id",
            table: "deployments",
            column: "feature_id");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "deployments");
        migrationBuilder.DropTable(name: "pipeline_events");
        migrationBuilder.DropTable(name: "task_items");
        migrationBuilder.DropTable(name: "artifacts");
        migrationBuilder.DropTable(name: "features");
        migrationBuilder.DropTable(name: "projects");
    }
}
