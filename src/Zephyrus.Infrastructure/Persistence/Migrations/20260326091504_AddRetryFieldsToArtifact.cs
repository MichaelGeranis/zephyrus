using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zephyrus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRetryFieldsToArtifact : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "commit_succeeded",
                table: "artifacts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "pending_content",
                table: "artifacts",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "commit_succeeded",
                table: "artifacts");

            migrationBuilder.DropColumn(
                name: "pending_content",
                table: "artifacts");
        }
    }
}
