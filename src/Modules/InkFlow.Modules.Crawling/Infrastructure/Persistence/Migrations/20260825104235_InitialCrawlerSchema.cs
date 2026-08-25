using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InkFlow.Modules.Crawling.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCrawlerSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "crawler");

            migrationBuilder.CreateTable(
                name: "dead_letters",
                schema: "crawler",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TaskId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Reason = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    DeadLetteredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dead_letters", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "tasks",
                schema: "crawler",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Capability = table.Column<int>(type: "integer", nullable: false),
                    Variables = table.Column<string>(type: "jsonb", nullable: false),
                    CredentialReferenceId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    MaxAttempts = table.Column<int>(type: "integer", nullable: false),
                    LeaseOwner = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    LeaseExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tasks", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_dead_letters_TaskId",
                schema: "crawler",
                table: "dead_letters",
                column: "TaskId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tasks_Status",
                schema: "crawler",
                table: "tasks",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_tasks_Status_LeaseExpiresAt",
                schema: "crawler",
                table: "tasks",
                columns: new[] { "Status", "LeaseExpiresAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "dead_letters",
                schema: "crawler");

            migrationBuilder.DropTable(
                name: "tasks",
                schema: "crawler");
        }
    }
}
