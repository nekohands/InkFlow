using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace InkFlow.BuildingBlocks.Persistence.Migrations.Sources;

[DbContext(typeof(SourcesDbContext))]
[Migration("20260820235000_InitialSources")]
public sealed class InitialSources : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        CREATE TABLE sources.sources (
            id uuid PRIMARY KEY,
            name varchar(256) NOT NULL,
            base_url varchar(2048) NOT NULL,
            kind varchar(32) NOT NULL,
            status varchar(32) NOT NULL,
            capabilities_json jsonb NOT NULL,
            active_rule_version_id uuid NULL,
            health_score double precision NOT NULL,
            created_at_utc timestamptz NOT NULL,
            updated_at_utc timestamptz NOT NULL
        );
        CREATE TABLE sources.rule_versions (
            id uuid PRIMARY KEY,
            source_id uuid NOT NULL,
            version integer NOT NULL,
            schema_version integer NOT NULL,
            status varchar(32) NOT NULL,
            rule_json jsonb NOT NULL,
            created_at_utc timestamptz NOT NULL,
            published_at_utc timestamptz NULL
        );
        CREATE UNIQUE INDEX ux_source_rule_versions_source_version ON sources.rule_versions(source_id, version);
        CREATE TABLE sources.source_books (
            id uuid PRIMARY KEY,
            source_id uuid NOT NULL,
            external_id varchar(512) NOT NULL,
            url varchar(2048) NOT NULL,
            title varchar(512) NOT NULL,
            author varchar(512) NOT NULL,
            description text NULL,
            latest_chapter_external_id varchar(512) NULL,
            last_checked_at_utc timestamptz NULL,
            last_updated_at_utc timestamptz NULL,
            created_at_utc timestamptz NOT NULL,
            updated_at_utc timestamptz NOT NULL
        );
        CREATE UNIQUE INDEX ux_source_books_source_external ON sources.source_books(source_id, external_id);
        CREATE TABLE sources.source_chapters (
            id uuid PRIMARY KEY,
            source_book_id uuid NOT NULL,
            external_id varchar(512) NOT NULL,
            url varchar(2048) NOT NULL,
            title varchar(512) NOT NULL,
            sequence bigint NOT NULL,
            published_at_utc timestamptz NULL,
            created_at_utc timestamptz NOT NULL,
            updated_at_utc timestamptz NOT NULL
        );
        CREATE UNIQUE INDEX ux_source_chapters_book_external ON sources.source_chapters(source_book_id, external_id);
        CREATE INDEX ix_source_chapters_book_sequence ON sources.source_chapters(source_book_id, sequence);
        """);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        DROP TABLE IF EXISTS sources.source_chapters;
        DROP TABLE IF EXISTS sources.source_books;
        DROP TABLE IF EXISTS sources.rule_versions;
        DROP TABLE IF EXISTS sources.sources;
        """);
}
