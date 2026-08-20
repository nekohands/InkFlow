using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace InkFlow.BuildingBlocks.Persistence.Migrations.Content;

[DbContext(typeof(ContentDbContext))]
[Migration("20260820235200_InitialContent")]
public sealed class InitialContent : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        CREATE TABLE content.content_blobs (
            id uuid PRIMARY KEY,
            content_hash varchar(64) NOT NULL,
            storage_kind varchar(32) NOT NULL,
            inline_content text NULL,
            object_key varchar(1024) NULL,
            size_bytes bigint NOT NULL,
            created_at_utc timestamptz NOT NULL
        );
        CREATE UNIQUE INDEX ux_content_blobs_hash ON content.content_blobs(content_hash);
        CREATE TABLE content.content_versions (
            id uuid PRIMARY KEY,
            chapter_id uuid NOT NULL,
            source_chapter_id uuid NOT NULL,
            blob_id uuid NOT NULL,
            raw_hash varchar(64) NOT NULL,
            canonical_hash varchar(64) NOT NULL,
            quality_score double precision NOT NULL,
            evidence_json jsonb NOT NULL,
            normalizer_version varchar(64) NOT NULL,
            created_at_utc timestamptz NOT NULL
        );
        CREATE UNIQUE INDEX ux_content_versions_source_hash ON content.content_versions(source_chapter_id, canonical_hash);
        CREATE INDEX ix_content_versions_chapter_quality ON content.content_versions(chapter_id, quality_score);
        CREATE TABLE content.chapter_selections (
            chapter_id uuid PRIMARY KEY,
            content_version_id uuid NOT NULL,
            is_locked boolean NOT NULL,
            reason varchar(256) NOT NULL,
            selected_at_utc timestamptz NOT NULL
        );
        """);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        DROP TABLE IF EXISTS content.chapter_selections;
        DROP TABLE IF EXISTS content.content_versions;
        DROP TABLE IF EXISTS content.content_blobs;
        """);
}
