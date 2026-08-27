using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InkFlow.Modules.Library.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddChapterAlignmentEvidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AlignmentAlgorithmVersion",
                schema: "library",
                table: "chapter_mappings",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "legacy");

            migrationBuilder.AddColumn<string>(
                name: "AlignmentEvidence",
                schema: "library",
                table: "chapter_mappings",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: false,
                defaultValue: "legacy");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AlignmentAlgorithmVersion",
                schema: "library",
                table: "chapter_mappings");

            migrationBuilder.DropColumn(
                name: "AlignmentEvidence",
                schema: "library",
                table: "chapter_mappings");
        }
    }
}
