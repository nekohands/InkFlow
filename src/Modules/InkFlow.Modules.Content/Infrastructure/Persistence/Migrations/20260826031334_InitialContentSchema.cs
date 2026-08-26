using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InkFlow.Modules.Content.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialContentSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "content");

            migrationBuilder.CreateTable(
                name: "versions",
                schema: "content",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CanonicalBookId = table.Column<Guid>(type: "uuid", nullable: false),
                    CanonicalChapterId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CanonicalHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CanonicalText = table.Column<string>(type: "text", nullable: false),
                    ParagraphCount = table.Column<int>(type: "integer", nullable: false),
                    QualityScore = table.Column<int>(type: "integer", nullable: false),
                    IsCurrent = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_versions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_versions_CanonicalChapterId_CanonicalHash",
                schema: "content",
                table: "versions",
                columns: new[] { "CanonicalChapterId", "CanonicalHash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_versions_CanonicalChapterId_IsCurrent",
                schema: "content",
                table: "versions",
                columns: new[] { "CanonicalChapterId", "IsCurrent" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "versions",
                schema: "content");
        }
    }
}
