using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zephyrus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class _20261109_AddTraceParentToAgentJobs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "trace_parent",
                table: "agent_jobs",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "trace_parent",
                table: "agent_jobs");
        }
    }
}
