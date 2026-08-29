using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InkFlow.Modules.Sources.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSourceDefaultCredentialReference : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DefaultCredentialReferenceId",
                schema: "sources",
                table: "sources",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DefaultCredentialReferenceId",
                schema: "sources",
                table: "sources");
        }
    }
}
