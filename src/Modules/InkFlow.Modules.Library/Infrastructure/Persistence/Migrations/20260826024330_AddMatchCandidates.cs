using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InkFlow.Modules.Library.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMatchCandidates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "match_candidates",
                schema: "library",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CanonicalBookId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ExternalBookId = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_match_candidates", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_match_candidates_CanonicalBookId",
                schema: "library",
                table: "match_candidates",
                column: "CanonicalBookId");

            migrationBuilder.CreateIndex(
                name: "IX_match_candidates_SourceId_ExternalBookId",
                schema: "library",
                table: "match_candidates",
                columns: new[] { "SourceId", "ExternalBookId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "match_candidates",
                schema: "library");
        }
    }
}
