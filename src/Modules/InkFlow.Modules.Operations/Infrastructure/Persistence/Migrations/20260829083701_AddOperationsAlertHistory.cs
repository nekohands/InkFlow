using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InkFlow.Modules.Operations.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOperationsAlertHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "operations");

            migrationBuilder.CreateTable(
                name: "alert_history",
                schema: "operations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Code = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Severity = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ResourceType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ResourceId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Transition = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    OccurrenceCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_alert_history", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "alert_incidents",
                schema: "operations",
                columns: table => new
                {
                    Fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Code = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Severity = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ResourceType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ResourceId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    FirstSeenAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastSeenAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastTransitionAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastResolvedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    OccurrenceCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_alert_incidents", x => x.Fingerprint);
                });

            migrationBuilder.CreateIndex(
                name: "IX_alert_history_Fingerprint_OccurredAt",
                schema: "operations",
                table: "alert_history",
                columns: new[] { "Fingerprint", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_alert_history_OccurredAt_Id",
                schema: "operations",
                table: "alert_history",
                columns: new[] { "OccurredAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_alert_incidents_Status_LastTransitionAt",
                schema: "operations",
                table: "alert_incidents",
                columns: new[] { "Status", "LastTransitionAt" });

            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION "operations"."prevent_alert_history_update"()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $function$
                BEGIN
                    RAISE EXCEPTION 'operations.alert_history is append-only';
                END;
                $function$;

                CREATE TRIGGER "TR_operations_alert_history_append_only"
                BEFORE UPDATE ON "operations"."alert_history"
                FOR EACH ROW
                EXECUTE FUNCTION "operations"."prevent_alert_history_update"();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "TR_operations_alert_history_append_only" ON "operations"."alert_history";
                DROP FUNCTION IF EXISTS "operations"."prevent_alert_history_update"();
                """);

            migrationBuilder.DropTable(
                name: "alert_history",
                schema: "operations");

            migrationBuilder.DropTable(
                name: "alert_incidents",
                schema: "operations");
        }
    }
}
