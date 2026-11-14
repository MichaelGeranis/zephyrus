using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zephyrus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class _20261114_AddContentRemovedAtToAgentInvocations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "content_removed_at",
                table: "agent_invocations",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "content_removed_at",
                table: "agent_invocations");
        }
    }
}
