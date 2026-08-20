using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace InkFlow.BuildingBlocks.Persistence.Migrations.Crawler;

[DbContext(typeof(CrawlingDbContext))]
[Migration("20260820235300_FetchArtifacts")]
public sealed class FetchArtifacts : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        CREATE TABLE crawler.fetch_artifacts (
            id uuid PRIMARY KEY,
            crawler_task_id uuid NULL,
            source_id uuid NOT NULL,
            source_chapter_id uuid NULL,
            rule_version_id uuid NULL,
            url varchar(2048) NOT NULL,
            status_code integer NOT NULL,
            headers_json jsonb NOT NULL,
            raw_hash varchar(64) NOT NULL,
            raw_body text NULL,
            parser_version varchar(64) NOT NULL,
            fetched_at_utc timestamptz NOT NULL
        );
        CREATE INDEX ix_fetch_artifacts_source_chapter ON crawler.fetch_artifacts(source_chapter_id);
        CREATE INDEX ix_fetch_artifacts_raw_hash ON crawler.fetch_artifacts(raw_hash);
        """);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("DROP TABLE IF EXISTS crawler.fetch_artifacts;");
}
