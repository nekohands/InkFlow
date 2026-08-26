using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InkFlow.Modules.Sources.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFetchArtifacts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "fetch_artifacts",
                schema: "sources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ExternalBookId = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    ExternalChapterId = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    RawHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    BodyLength = table.Column<int>(type: "integer", nullable: false),
                    FetchedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fetch_artifacts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_fetch_artifacts_SourceId_ExternalChapterId_FetchedAt",
                schema: "sources",
                table: "fetch_artifacts",
                columns: new[] { "SourceId", "ExternalChapterId", "FetchedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "fetch_artifacts",
                schema: "sources");
        }
    }
}
