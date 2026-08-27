using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InkFlow.Modules.Content.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddContentPolicyDecisions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "policy_decisions",
                schema: "content",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CanonicalBookId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<int>(type: "integer", nullable: false),
                    ActorId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Reason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_policy_decisions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_policy_decisions_CanonicalBookId_CreatedAt_Id",
                schema: "content",
                table: "policy_decisions",
                columns: new[] { "CanonicalBookId", "CreatedAt", "Id" });

            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION "content"."prevent_policy_decision_mutation"()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $function$
                BEGIN
                    RAISE EXCEPTION 'content.policy_decisions is append-only';
                END;
                $function$;

                CREATE TRIGGER "TR_content_policy_decisions_append_only"
                BEFORE UPDATE OR DELETE ON "content"."policy_decisions"
                FOR EACH ROW
                EXECUTE FUNCTION "content"."prevent_policy_decision_mutation"();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "TR_content_policy_decisions_append_only"
                    ON "content"."policy_decisions";
                DROP FUNCTION IF EXISTS "content"."prevent_policy_decision_mutation"();
                """);

            migrationBuilder.DropTable(
                name: "policy_decisions",
                schema: "content");
        }
    }
}
