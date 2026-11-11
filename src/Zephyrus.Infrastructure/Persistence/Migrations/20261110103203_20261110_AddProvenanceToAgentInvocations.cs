using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zephyrus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class _20261110_AddProvenanceToAgentInvocations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "artifact_id",
                table: "agent_invocations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "prompt_version",
                table: "agent_invocations",
                type: "character varying(12)",
                maxLength: 12,
                nullable: false,
                defaultValue: "");

            // Earlier runs: the same version AgentInvocation.VersionOf computes
            // (first 12 hex characters of the SHA-256 of the system prompt).
            migrationBuilder.Sql(
                "UPDATE agent_invocations " +
                "SET prompt_version = left(encode(sha256(convert_to(system_prompt, 'UTF8')), 'hex'), 12) " +
                "WHERE prompt_version = '';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "artifact_id",
                table: "agent_invocations");

            migrationBuilder.DropColumn(
                name: "prompt_version",
                table: "agent_invocations");
        }
    }
}
