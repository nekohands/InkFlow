using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InkFlow.Modules.Crawling.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDeadLetterReplay : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReplayReason",
                schema: "crawler",
                table: "dead_letters",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReplayRequestedBy",
                schema: "crawler",
                table: "dead_letters",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReplayTaskId",
                schema: "crawler",
                table: "dead_letters",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReplayedAt",
                schema: "crawler",
                table: "dead_letters",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_dead_letters_ReplayTaskId",
                schema: "crawler",
                table: "dead_letters",
                column: "ReplayTaskId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_dead_letters_ReplayTaskId",
                schema: "crawler",
                table: "dead_letters");

            migrationBuilder.DropColumn(
                name: "ReplayReason",
                schema: "crawler",
                table: "dead_letters");

            migrationBuilder.DropColumn(
                name: "ReplayRequestedBy",
                schema: "crawler",
                table: "dead_letters");

            migrationBuilder.DropColumn(
                name: "ReplayTaskId",
                schema: "crawler",
                table: "dead_letters");

            migrationBuilder.DropColumn(
                name: "ReplayedAt",
                schema: "crawler",
                table: "dead_letters");
        }
    }
}
