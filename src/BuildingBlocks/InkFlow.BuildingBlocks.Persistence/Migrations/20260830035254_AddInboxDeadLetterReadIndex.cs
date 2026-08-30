using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InkFlow.BuildingBlocks.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInboxDeadLetterReadIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_inbox_messages_ProcessedAt_DeadLetteredAt_Id",
                schema: "messaging",
                table: "inbox_messages",
                columns: new[] { "ProcessedAt", "DeadLetteredAt", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_inbox_messages_ProcessedAt_DeadLetteredAt_Id",
                schema: "messaging",
                table: "inbox_messages");
        }
    }
}
