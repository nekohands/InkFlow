using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InkFlow.Modules.Crawling.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCollectionRuns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "RunId",
                schema: "crawler",
                table: "tasks",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "runs",
                schema: "crawler",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ExternalBookId = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    InputUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    CanonicalBookId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Stage = table.Column<int>(type: "integer", nullable: false),
                    TotalTaskCount = table.Column<int>(type: "integer", nullable: false),
                    CompletedTaskCount = table.Column<int>(type: "integer", nullable: false),
                    FailedTaskCount = table.Column<int>(type: "integer", nullable: false),
                    LastError = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_runs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_tasks_RunId",
                schema: "crawler",
                table: "tasks",
                column: "RunId");

            migrationBuilder.CreateIndex(
                name: "IX_runs_SourceId_ExternalBookId_CreatedAt",
                schema: "crawler",
                table: "runs",
                columns: new[] { "SourceId", "ExternalBookId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_runs_Status_UpdatedAt",
                schema: "crawler",
                table: "runs",
                columns: new[] { "Status", "UpdatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "runs",
                schema: "crawler");

            migrationBuilder.DropIndex(
                name: "IX_tasks_RunId",
                schema: "crawler",
                table: "tasks");

            migrationBuilder.DropColumn(
                name: "RunId",
                schema: "crawler",
                table: "tasks");
        }
    }
}
