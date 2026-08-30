using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InkFlow.BuildingBlocks.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInboxConsumerPolling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "OccurredAt",
                schema: "messaging",
                table: "inbox_messages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_inbox_messages_MessageType_ProcessedAt_LockedUntil_Received~",
                schema: "messaging",
                table: "inbox_messages",
                columns: new[] { "MessageType", "ProcessedAt", "LockedUntil", "ReceivedAt", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_inbox_messages_MessageType_ProcessedAt_LockedUntil_Received~",
                schema: "messaging",
                table: "inbox_messages");

            migrationBuilder.DropColumn(
                name: "OccurredAt",
                schema: "messaging",
                table: "inbox_messages");
        }
    }
}
