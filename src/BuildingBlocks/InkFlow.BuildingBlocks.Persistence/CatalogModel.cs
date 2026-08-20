using Microsoft.EntityFrameworkCore;

namespace InkFlow.BuildingBlocks.Persistence;

internal static class CatalogModel
{
    public static void ConfigureSources(ModelBuilder modelBuilder)
    {
        var source = modelBuilder.Entity<SourceRecord>();
        source.ToTable("sources", DatabaseSchemas.Sources);
        source.HasKey(x => x.Id).HasName("pk_sources");
        source.Property(x => x.Id).HasColumnName("id");
        source.Property(x => x.Name).HasColumnName("name").HasMaxLength(256).IsRequired();
        source.Property(x => x.BaseUrl).HasColumnName("base_url").HasMaxLength(2048).IsRequired();
        source.Property(x => x.Kind).HasColumnName("kind").HasMaxLength(32).IsRequired();
        source.Property(x => x.Status).HasColumnName("status").HasMaxLength(32).IsRequired();
        source.Property(x => x.CapabilitiesJson).HasColumnName("capabilities_json").HasColumnType("jsonb").IsRequired();
        source.Property(x => x.ActiveRuleVersionId).HasColumnName("active_rule_version_id");
        source.Property(x => x.HealthScore).HasColumnName("health_score");
        source.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");
        source.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc");

        var rule = modelBuilder.Entity<SourceRuleVersionRecord>();
        rule.ToTable("rule_versions", DatabaseSchemas.Sources);
        rule.HasKey(x => x.Id).HasName("pk_source_rule_versions");
        rule.Property(x => x.Id).HasColumnName("id");
        rule.Property(x => x.SourceId).HasColumnName("source_id");
        rule.Property(x => x.Version).HasColumnName("version");
        rule.Property(x => x.SchemaVersion).HasColumnName("schema_version");
        rule.Property(x => x.Status).HasColumnName("status").HasMaxLength(32).IsRequired();
        rule.Property(x => x.RuleJson).HasColumnName("rule_json").HasColumnType("jsonb").IsRequired();
        rule.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");
        rule.Property(x => x.PublishedAtUtc).HasColumnName("published_at_utc");
        rule.HasIndex(x => new { x.SourceId, x.Version }).IsUnique().HasDatabaseName("ux_source_rule_versions_source_version");

        var book = modelBuilder.Entity<SourceBookRecord>();
        book.ToTable("source_books", DatabaseSchemas.Sources);
        book.HasKey(x => x.Id).HasName("pk_source_books");
        book.Property(x => x.Id).HasColumnName("id");
        book.Property(x => x.SourceId).HasColumnName("source_id");
        book.Property(x => x.ExternalId).HasColumnName("external_id").HasMaxLength(512).IsRequired();
        book.Property(x => x.Url).HasColumnName("url").HasMaxLength(2048).IsRequired();
        book.Property(x => x.Title).HasColumnName("title").HasMaxLength(512).IsRequired();
        book.Property(x => x.Author).HasColumnName("author").HasMaxLength(512).IsRequired();
        book.Property(x => x.Description).HasColumnName("description");
        book.Property(x => x.LatestChapterExternalId).HasColumnName("latest_chapter_external_id").HasMaxLength(512);
        book.Property(x => x.LastCheckedAtUtc).HasColumnName("last_checked_at_utc");
        book.Property(x => x.LastUpdatedAtUtc).HasColumnName("last_updated_at_utc");
        book.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");
        book.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc");
        book.HasIndex(x => new { x.SourceId, x.ExternalId }).IsUnique().HasDatabaseName("ux_source_books_source_external");

        var chapter = modelBuilder.Entity<SourceChapterRecord>();
        chapter.ToTable("source_chapters", DatabaseSchemas.Sources);
        chapter.HasKey(x => x.Id).HasName("pk_source_chapters");
        chapter.Property(x => x.Id).HasColumnName("id");
        chapter.Property(x => x.SourceBookId).HasColumnName("source_book_id");
        chapter.Property(x => x.ExternalId).HasColumnName("external_id").HasMaxLength(512).IsRequired();
        chapter.Property(x => x.Url).HasColumnName("url").HasMaxLength(2048).IsRequired();
        chapter.Property(x => x.Title).HasColumnName("title").HasMaxLength(512).IsRequired();
        chapter.Property(x => x.Sequence).HasColumnName("sequence");
        chapter.Property(x => x.PublishedAtUtc).HasColumnName("published_at_utc");
        chapter.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");
        chapter.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc");
        chapter.HasIndex(x => new { x.SourceBookId, x.ExternalId }).IsUnique().HasDatabaseName("ux_source_chapters_book_external");
        chapter.HasIndex(x => new { x.SourceBookId, x.Sequence }).HasDatabaseName("ix_source_chapters_book_sequence");
    }

    public static void ConfigureLibrary(ModelBuilder modelBuilder)
    {
        var book = modelBuilder.Entity<BookRecord>();
        book.ToTable("books", DatabaseSchemas.Library);
        book.HasKey(x => x.Id).HasName("pk_books");
        book.Property(x => x.Id).HasColumnName("id");
        book.Property(x => x.Title).HasColumnName("title").HasMaxLength(512).IsRequired();
        book.Property(x => x.NormalizedTitle).HasColumnName("normalized_title").HasMaxLength(512).IsRequired();
        book.Property(x => x.Author).HasColumnName("author").HasMaxLength(512).IsRequired();
        book.Property(x => x.NormalizedAuthor).HasColumnName("normalized_author").HasMaxLength(512).IsRequired();
        book.Property(x => x.Description).HasColumnName("description");
        book.Property(x => x.Status).HasColumnName("status").HasMaxLength(32).IsRequired();
        book.Property(x => x.Revision).HasColumnName("revision");
        book.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");
        book.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc");
        book.HasIndex(x => new { x.NormalizedTitle, x.NormalizedAuthor }).HasDatabaseName("ix_books_normalized_identity");

        var chapter = modelBuilder.Entity<ChapterRecord>();
        chapter.ToTable("chapters", DatabaseSchemas.Library);
        chapter.HasKey(x => x.Id).HasName("pk_chapters");
        chapter.Property(x => x.Id).HasColumnName("id");
        chapter.Property(x => x.BookId).HasColumnName("book_id");
        chapter.Property(x => x.Sequence).HasColumnName("sequence");
        chapter.Property(x => x.DisplayNumber).HasColumnName("display_number");
        chapter.Property(x => x.Title).HasColumnName("title").HasMaxLength(512).IsRequired();
        chapter.Property(x => x.NormalizedTitle).HasColumnName("normalized_title").HasMaxLength(512).IsRequired();
        chapter.Property(x => x.Revision).HasColumnName("revision");
        chapter.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");
        chapter.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc");
        chapter.HasIndex(x => new { x.BookId, x.Sequence }).IsUnique().HasDatabaseName("ux_chapters_book_sequence");

        var match = modelBuilder.Entity<SourceBookMatchRecord>();
        match.ToTable("source_book_matches", DatabaseSchemas.Library);
        match.HasKey(x => x.Id).HasName("pk_source_book_matches");
        match.Property(x => x.Id).HasColumnName("id");
        match.Property(x => x.BookId).HasColumnName("book_id");
        match.Property(x => x.SourceBookId).HasColumnName("source_book_id");
        match.Property(x => x.Score).HasColumnName("score");
        match.Property(x => x.EvidenceJson).HasColumnName("evidence_json").HasColumnType("jsonb").IsRequired();
        match.Property(x => x.AlgorithmVersion).HasColumnName("algorithm_version").HasMaxLength(64).IsRequired();
        match.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");
        match.HasIndex(x => x.SourceBookId).IsUnique().HasDatabaseName("ux_source_book_matches_source_book");

        var mapping = modelBuilder.Entity<ChapterMappingRecord>();
        mapping.ToTable("chapter_mappings", DatabaseSchemas.Library);
        mapping.HasKey(x => x.Id).HasName("pk_chapter_mappings");
        mapping.Property(x => x.Id).HasColumnName("id");
        mapping.Property(x => x.ChapterId).HasColumnName("chapter_id");
        mapping.Property(x => x.SourceChapterId).HasColumnName("source_chapter_id");
        mapping.Property(x => x.Score).HasColumnName("score");
        mapping.Property(x => x.EvidenceJson).HasColumnName("evidence_json").HasColumnType("jsonb").IsRequired();
        mapping.Property(x => x.AlgorithmVersion).HasColumnName("algorithm_version").HasMaxLength(64).IsRequired();
        mapping.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");
        mapping.HasIndex(x => new { x.ChapterId, x.SourceChapterId }).IsUnique().HasDatabaseName("ux_chapter_mappings_pair");
        mapping.HasIndex(x => x.SourceChapterId).HasDatabaseName("ix_chapter_mappings_source_chapter");
    }

    public static void ConfigureContent(ModelBuilder modelBuilder)
    {
        var blob = modelBuilder.Entity<ContentBlobRecord>();
        blob.ToTable("content_blobs", DatabaseSchemas.Content);
        blob.HasKey(x => x.Id).HasName("pk_content_blobs");
        blob.Property(x => x.Id).HasColumnName("id");
        blob.Property(x => x.ContentHash).HasColumnName("content_hash").HasMaxLength(64).IsRequired();
        blob.Property(x => x.StorageKind).HasColumnName("storage_kind").HasMaxLength(32).IsRequired();
        blob.Property(x => x.InlineContent).HasColumnName("inline_content");
        blob.Property(x => x.ObjectKey).HasColumnName("object_key").HasMaxLength(1024);
        blob.Property(x => x.SizeBytes).HasColumnName("size_bytes");
        blob.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");
        blob.HasIndex(x => x.ContentHash).IsUnique().HasDatabaseName("ux_content_blobs_hash");

        var version = modelBuilder.Entity<ContentVersionRecord>();
        version.ToTable("content_versions", DatabaseSchemas.Content);
        version.HasKey(x => x.Id).HasName("pk_content_versions");
        version.Property(x => x.Id).HasColumnName("id");
        version.Property(x => x.ChapterId).HasColumnName("chapter_id");
        version.Property(x => x.SourceChapterId).HasColumnName("source_chapter_id");
        version.Property(x => x.BlobId).HasColumnName("blob_id");
        version.Property(x => x.RawHash).HasColumnName("raw_hash").HasMaxLength(64).IsRequired();
        version.Property(x => x.CanonicalHash).HasColumnName("canonical_hash").HasMaxLength(64).IsRequired();
        version.Property(x => x.QualityScore).HasColumnName("quality_score");
        version.Property(x => x.EvidenceJson).HasColumnName("evidence_json").HasColumnType("jsonb").IsRequired();
        version.Property(x => x.NormalizerVersion).HasColumnName("normalizer_version").HasMaxLength(64).IsRequired();
        version.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");
        version.HasIndex(x => new { x.SourceChapterId, x.CanonicalHash }).IsUnique().HasDatabaseName("ux_content_versions_source_hash");
        version.HasIndex(x => new { x.ChapterId, x.QualityScore }).HasDatabaseName("ix_content_versions_chapter_quality");

        var selection = modelBuilder.Entity<ChapterSelectionRecord>();
        selection.ToTable("chapter_selections", DatabaseSchemas.Content);
        selection.HasKey(x => x.ChapterId).HasName("pk_chapter_selections");
        selection.Property(x => x.ChapterId).HasColumnName("chapter_id");
        selection.Property(x => x.ContentVersionId).HasColumnName("content_version_id");
        selection.Property(x => x.IsLocked).HasColumnName("is_locked");
        selection.Property(x => x.Reason).HasColumnName("reason").HasMaxLength(256).IsRequired();
        selection.Property(x => x.SelectedAtUtc).HasColumnName("selected_at_utc");
    }

    public static void ConfigureFetchArtifacts(ModelBuilder modelBuilder)
    {
        var artifact = modelBuilder.Entity<FetchArtifactRecord>();
        artifact.ToTable("fetch_artifacts", DatabaseSchemas.Crawler);
        artifact.HasKey(x => x.Id).HasName("pk_fetch_artifacts");
        artifact.Property(x => x.Id).HasColumnName("id");
        artifact.Property(x => x.CrawlerTaskId).HasColumnName("crawler_task_id");
        artifact.Property(x => x.SourceId).HasColumnName("source_id");
        artifact.Property(x => x.SourceChapterId).HasColumnName("source_chapter_id");
        artifact.Property(x => x.RuleVersionId).HasColumnName("rule_version_id");
        artifact.Property(x => x.Url).HasColumnName("url").HasMaxLength(2048).IsRequired();
        artifact.Property(x => x.StatusCode).HasColumnName("status_code");
        artifact.Property(x => x.HeadersJson).HasColumnName("headers_json").HasColumnType("jsonb").IsRequired();
        artifact.Property(x => x.RawHash).HasColumnName("raw_hash").HasMaxLength(64).IsRequired();
        artifact.Property(x => x.RawBody).HasColumnName("raw_body");
        artifact.Property(x => x.ParserVersion).HasColumnName("parser_version").HasMaxLength(64).IsRequired();
        artifact.Property(x => x.FetchedAtUtc).HasColumnName("fetched_at_utc");
        artifact.HasIndex(x => x.SourceChapterId).HasDatabaseName("ix_fetch_artifacts_source_chapter");
        artifact.HasIndex(x => x.RawHash).HasDatabaseName("ix_fetch_artifacts_raw_hash");
    }
}
