using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InkFlow.Modules.Crawling.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EnforceActiveCollectionRunUniqueness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "UX_runs_active_source_book",
                schema: "crawler",
                table: "runs",
                columns: new[] { "SourceId", "ExternalBookId" },
                unique: true,
                filter: "\"Status\" IN (0, 1, 2, 3)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_runs_active_source_book",
                schema: "crawler",
                table: "runs");
        }
    }
}
