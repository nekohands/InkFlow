using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace InkFlow.BuildingBlocks.Persistence.Migrations.Crawler;

[DbContext(typeof(CrawlingDbContext))]
[Migration("20260820233500_InitialCrawlerTasks")]
public sealed class InitialCrawlerTasks : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "tasks",
            schema: DatabaseSchemas.Crawler,
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                type = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                source_id = table.Column<Guid>(type: "uuid", nullable: true),
                payload = table.Column<string>(type: "jsonb", nullable: false),
                idempotency_key = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                priority = table.Column<int>(type: "integer", nullable: false),
                status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                attempt = table.Column<int>(type: "integer", nullable: false),
                max_attempts = table.Column<int>(type: "integer", nullable: false),
                scheduled_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                lease_until_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                lease_owner = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                last_error = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                trace_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true)
            },
            constraints: table => table.PrimaryKey("pk_crawler_tasks", x => x.id));

        migrationBuilder.CreateIndex(
            name: "ux_crawler_tasks_idempotency_key",
            schema: DatabaseSchemas.Crawler,
            table: "tasks",
            column: "idempotency_key",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_crawler_tasks_dispatch",
            schema: DatabaseSchemas.Crawler,
            table: "tasks",
            columns: ["status", "scheduled_at_utc", "priority"]);

        migrationBuilder.CreateIndex(
            name: "ix_crawler_tasks_lease_until",
            schema: DatabaseSchemas.Crawler,
            table: "tasks",
            column: "lease_until_utc");
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropTable(name: "tasks", schema: DatabaseSchemas.Crawler);
}
