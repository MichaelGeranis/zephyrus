using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zephyrus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class _20261109_AddUsageToAgentInvocations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "estimated_cost_usd",
                table: "agent_invocations",
                type: "numeric(12,6)",
                precision: 12,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "input_tokens",
                table: "agent_invocations",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "model",
                table: "agent_invocations",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "output_tokens",
                table: "agent_invocations",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "estimated_cost_usd",
                table: "agent_invocations");

            migrationBuilder.DropColumn(
                name: "input_tokens",
                table: "agent_invocations");

            migrationBuilder.DropColumn(
                name: "model",
                table: "agent_invocations");

            migrationBuilder.DropColumn(
                name: "output_tokens",
                table: "agent_invocations");
        }
    }
}
