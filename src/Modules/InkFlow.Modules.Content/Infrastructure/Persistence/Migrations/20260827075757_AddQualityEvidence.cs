using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InkFlow.Modules.Content.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddQualityEvidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "QualityAlgorithmVersion",
                schema: "content",
                table: "versions",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "quality-v1");

            migrationBuilder.AddColumn<string>(
                name: "QualityEvidence",
                schema: "content",
                table: "versions",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: false,
                defaultValue: "legacy");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "QualityAlgorithmVersion",
                schema: "content",
                table: "versions");

            migrationBuilder.DropColumn(
                name: "QualityEvidence",
                schema: "content",
                table: "versions");
        }
    }
}
