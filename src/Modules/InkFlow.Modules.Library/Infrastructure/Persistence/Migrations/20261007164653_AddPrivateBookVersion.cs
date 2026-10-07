using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InkFlow.Modules.Library.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPrivateBookVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "Version",
                schema: "library",
                table: "private_books",
                type: "bigint",
                nullable: false,
                defaultValue: 1L);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Version",
                schema: "library",
                table: "private_books");
        }
    }
}
