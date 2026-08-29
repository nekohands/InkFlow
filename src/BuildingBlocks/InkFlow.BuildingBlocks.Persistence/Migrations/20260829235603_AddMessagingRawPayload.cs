using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InkFlow.BuildingBlocks.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMessagingRawPayload : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RawPayload",
                schema: "messaging",
                table: "outbox_messages",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RawPayload",
                schema: "messaging",
                table: "inbox_messages",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RawPayload",
                schema: "messaging",
                table: "outbox_messages");

            migrationBuilder.DropColumn(
                name: "RawPayload",
                schema: "messaging",
                table: "inbox_messages");
        }
    }
}
