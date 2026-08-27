using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InkFlow.Modules.Crawling.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCrawlerTaskScheduling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ScheduledAt",
                schema: "crawler",
                table: "tasks",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_tasks_Status_ScheduledAt",
                schema: "crawler",
                table: "tasks",
                columns: new[] { "Status", "ScheduledAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_tasks_Status_ScheduledAt",
                schema: "crawler",
                table: "tasks");

            migrationBuilder.DropColumn(
                name: "ScheduledAt",
                schema: "crawler",
                table: "tasks");
        }
    }
}
