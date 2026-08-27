using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InkFlow.Modules.Reading.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReadingState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "reading");

            migrationBuilder.CreateTable(
                name: "history",
                schema: "reading",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CanonicalBookId = table.Column<Guid>(type: "uuid", nullable: false),
                    CanonicalChapterId = table.Column<Guid>(type: "uuid", nullable: false),
                    FirstReadAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastReadAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_history", x => new { x.UserId, x.CanonicalBookId, x.CanonicalChapterId });
                });

            migrationBuilder.CreateTable(
                name: "preferences",
                schema: "reading",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    FontSizePercent = table.Column<int>(type: "integer", nullable: false),
                    LineHeightPercent = table.Column<int>(type: "integer", nullable: false),
                    Theme = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_preferences", x => x.UserId);
                });

            migrationBuilder.CreateTable(
                name: "progress",
                schema: "reading",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CanonicalBookId = table.Column<Guid>(type: "uuid", nullable: false),
                    CanonicalChapterId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParagraphIndex = table.Column<int>(type: "integer", nullable: false),
                    ProgressPercent = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_progress", x => new { x.UserId, x.CanonicalBookId });
                });

            migrationBuilder.CreateTable(
                name: "shelf_entries",
                schema: "reading",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CanonicalBookId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    AddedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shelf_entries", x => new { x.UserId, x.CanonicalBookId });
                });

            migrationBuilder.CreateIndex(
                name: "IX_history_UserId_LastReadAt",
                schema: "reading",
                table: "history",
                columns: new[] { "UserId", "LastReadAt" });

            migrationBuilder.CreateIndex(
                name: "IX_progress_UserId_UpdatedAt",
                schema: "reading",
                table: "progress",
                columns: new[] { "UserId", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_shelf_entries_UserId_UpdatedAt",
                schema: "reading",
                table: "shelf_entries",
                columns: new[] { "UserId", "UpdatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "history",
                schema: "reading");

            migrationBuilder.DropTable(
                name: "preferences",
                schema: "reading");

            migrationBuilder.DropTable(
                name: "progress",
                schema: "reading");

            migrationBuilder.DropTable(
                name: "shelf_entries",
                schema: "reading");
        }
    }
}
