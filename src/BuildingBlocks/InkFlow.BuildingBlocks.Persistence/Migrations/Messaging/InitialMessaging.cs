using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace InkFlow.BuildingBlocks.Persistence.Migrations.Messaging;

[DbContext(typeof(MessagingDbContext))]
[Migration("20260820151000_InitialMessaging")]
public sealed class InitialMessaging : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "outbox_messages",
            schema: DatabaseSchemas.Messaging,
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                OccurredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                Type = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                Payload = table.Column<string>(type: "jsonb", nullable: false),
                ProcessedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                Attempts = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                LastError = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_outbox_messages", row => row.Id));

        migrationBuilder.CreateIndex(
            name: "IX_outbox_messages_ProcessedAtUtc_OccurredAtUtc",
            schema: DatabaseSchemas.Messaging,
            table: "outbox_messages",
            columns: new[] { "ProcessedAtUtc", "OccurredAtUtc" });

        migrationBuilder.CreateTable(
            name: "inbox_messages",
            schema: DatabaseSchemas.Messaging,
            columns: table => new
            {
                MessageId = table.Column<Guid>(type: "uuid", nullable: false),
                Consumer = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                ProcessedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_inbox_messages", row => new { row.MessageId, row.Consumer }));

        migrationBuilder.CreateIndex(
            name: "IX_inbox_messages_ProcessedAtUtc",
            schema: DatabaseSchemas.Messaging,
            table: "inbox_messages",
            column: "ProcessedAtUtc");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "inbox_messages", schema: DatabaseSchemas.Messaging);
        migrationBuilder.DropTable(name: "outbox_messages", schema: DatabaseSchemas.Messaging);
    }
}
