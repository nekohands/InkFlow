using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InkFlow.Modules.Sources.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialSourceBooksSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "source_books",
                schema: "sources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ExternalBookId = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Title = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Author = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_source_books", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "source_chapters",
                schema: "sources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceBookId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExternalChapterId = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    ChapterIndex = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_source_chapters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_source_chapters_source_books_SourceBookId",
                        column: x => x.SourceBookId,
                        principalSchema: "sources",
                        principalTable: "source_books",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_source_books_SourceId_ExternalBookId",
                schema: "sources",
                table: "source_books",
                columns: new[] { "SourceId", "ExternalBookId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_source_chapters_SourceBookId_ChapterIndex",
                schema: "sources",
                table: "source_chapters",
                columns: new[] { "SourceBookId", "ChapterIndex" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_source_chapters_SourceBookId_ExternalChapterId",
                schema: "sources",
                table: "source_chapters",
                columns: new[] { "SourceBookId", "ExternalChapterId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "source_chapters",
                schema: "sources");

            migrationBuilder.DropTable(
                name: "source_books",
                schema: "sources");
        }
    }
}
