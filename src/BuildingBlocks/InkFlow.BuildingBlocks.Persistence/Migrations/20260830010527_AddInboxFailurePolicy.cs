using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InkFlow.BuildingBlocks.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInboxFailurePolicy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_inbox_messages_MessageType_ProcessedAt_LockedUntil_Received~",
                schema: "messaging",
                table: "inbox_messages");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "AvailableAt",
                schema: "messaging",
                table: "inbox_messages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeadLetteredAt",
                schema: "messaging",
                table: "inbox_messages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_inbox_messages_MessageType_ProcessedAt_DeadLetteredAt_Avail~",
                schema: "messaging",
                table: "inbox_messages",
                columns: new[] { "MessageType", "ProcessedAt", "DeadLetteredAt", "AvailableAt", "LockedUntil", "ReceivedAt", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_inbox_messages_MessageType_ProcessedAt_DeadLetteredAt_Avail~",
                schema: "messaging",
                table: "inbox_messages");

            migrationBuilder.DropColumn(
                name: "AvailableAt",
                schema: "messaging",
                table: "inbox_messages");

            migrationBuilder.DropColumn(
                name: "DeadLetteredAt",
                schema: "messaging",
                table: "inbox_messages");

            migrationBuilder.CreateIndex(
                name: "IX_inbox_messages_MessageType_ProcessedAt_LockedUntil_Received~",
                schema: "messaging",
                table: "inbox_messages",
                columns: new[] { "MessageType", "ProcessedAt", "LockedUntil", "ReceivedAt", "Id" });
        }
    }
}
