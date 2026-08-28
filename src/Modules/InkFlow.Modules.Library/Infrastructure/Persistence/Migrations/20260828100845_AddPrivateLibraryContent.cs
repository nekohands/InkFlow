using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InkFlow.Modules.Library.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPrivateLibraryContent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "private_chapters",
                schema: "library",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PrivateBookId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChapterIndex = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    ContentText = table.Column<string>(type: "text", nullable: false),
                    ContentHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ParagraphCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_private_chapters", x => new { x.UserId, x.Id });
                    table.ForeignKey(
                        name: "FK_private_chapters_private_books_UserId_PrivateBookId",
                        columns: x => new { x.UserId, x.PrivateBookId },
                        principalSchema: "library",
                        principalTable: "private_books",
                        principalColumns: new[] { "UserId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_private_chapters_UserId_PrivateBookId_ChapterIndex",
                schema: "library",
                table: "private_chapters",
                columns: new[] { "UserId", "PrivateBookId", "ChapterIndex" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "private_chapters",
                schema: "library");
        }
    }
}
