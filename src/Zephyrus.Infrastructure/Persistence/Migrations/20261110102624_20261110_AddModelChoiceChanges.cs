using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zephyrus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class _20261110_AddModelChoiceChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "model_choice_changes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    stage = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    primary_model = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    fallback_model = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    changed_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    changed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_model_choice_changes", x => x.id);
                    table.ForeignKey(
                        name: "FK_model_choice_changes_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_model_choice_changes_project_id_changed_at",
                table: "model_choice_changes",
                columns: new[] { "project_id", "changed_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "model_choice_changes");
        }
    }
}
