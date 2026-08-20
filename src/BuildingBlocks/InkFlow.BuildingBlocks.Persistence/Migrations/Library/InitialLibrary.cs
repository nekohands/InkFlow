using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace InkFlow.BuildingBlocks.Persistence.Migrations.Library;

[DbContext(typeof(LibraryDbContext))]
[Migration("20260820235100_InitialLibrary")]
public sealed class InitialLibrary : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        CREATE TABLE library.books (
            id uuid PRIMARY KEY,
            title varchar(512) NOT NULL,
            normalized_title varchar(512) NOT NULL,
            author varchar(512) NOT NULL,
            normalized_author varchar(512) NOT NULL,
            description text NULL,
            status varchar(32) NOT NULL,
            revision bigint NOT NULL,
            created_at_utc timestamptz NOT NULL,
            updated_at_utc timestamptz NOT NULL
        );
        CREATE INDEX ix_books_normalized_identity ON library.books(normalized_title, normalized_author);
        CREATE TABLE library.chapters (
            id uuid PRIMARY KEY,
            book_id uuid NOT NULL,
            sequence bigint NOT NULL,
            display_number integer NULL,
            title varchar(512) NOT NULL,
            normalized_title varchar(512) NOT NULL,
            revision bigint NOT NULL,
            created_at_utc timestamptz NOT NULL,
            updated_at_utc timestamptz NOT NULL
        );
        CREATE UNIQUE INDEX ux_chapters_book_sequence ON library.chapters(book_id, sequence);
        CREATE TABLE library.source_book_matches (
            id uuid PRIMARY KEY,
            book_id uuid NOT NULL,
            source_book_id uuid NOT NULL,
            score double precision NOT NULL,
            evidence_json jsonb NOT NULL,
            algorithm_version varchar(64) NOT NULL,
            created_at_utc timestamptz NOT NULL
        );
        CREATE UNIQUE INDEX ux_source_book_matches_source_book ON library.source_book_matches(source_book_id);
        CREATE TABLE library.chapter_mappings (
            id uuid PRIMARY KEY,
            chapter_id uuid NOT NULL,
            source_chapter_id uuid NOT NULL,
            score double precision NOT NULL,
            evidence_json jsonb NOT NULL,
            algorithm_version varchar(64) NOT NULL,
            created_at_utc timestamptz NOT NULL
        );
        CREATE UNIQUE INDEX ux_chapter_mappings_pair ON library.chapter_mappings(chapter_id, source_chapter_id);
        CREATE INDEX ix_chapter_mappings_source_chapter ON library.chapter_mappings(source_chapter_id);
        """);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        DROP TABLE IF EXISTS library.chapter_mappings;
        DROP TABLE IF EXISTS library.source_book_matches;
        DROP TABLE IF EXISTS library.chapters;
        DROP TABLE IF EXISTS library.books;
        """);
}
