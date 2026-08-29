using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InkFlow.BuildingBlocks.Persistence.Migrations;

/// <summary>为有界审计 retention 开放事务级受控删除，普通更新/删除仍被追加式触发器拒绝。</summary>
public partial class AllowControlledAuditRetentionDeletes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE OR REPLACE FUNCTION "audit"."prevent_event_mutation"()
            RETURNS trigger
            LANGUAGE plpgsql
            AS $function$
            BEGIN
                IF TG_OP = 'DELETE'
                   AND current_setting('inkflow.audit_retention_cleanup', true) = 'on' THEN
                    RETURN OLD;
                END IF;

                RAISE EXCEPTION 'audit.events is append-only';
            END;
            $function$;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE OR REPLACE FUNCTION "audit"."prevent_event_mutation"()
            RETURNS trigger
            LANGUAGE plpgsql
            AS $function$
            BEGIN
                RAISE EXCEPTION 'audit.events is append-only';
            END;
            $function$;
            """);
    }
}
