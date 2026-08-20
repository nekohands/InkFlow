using InkFlow.BuildingBlocks.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Testcontainers.PostgreSql;

namespace InkFlow.IntegrationTests;

[TestClass]
public sealed class CatalogPersistenceTests
{
    [TestMethod]
    public async Task Canonical_source_and_content_records_round_trip_after_migration()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:18")
            .WithDatabase("inkflow").WithUsername("inkflow").WithPassword("inkflow-test").Build();
        await postgres.StartAsync();

        var services = new ServiceCollection();
        services.AddInkFlowPersistence(postgres.GetConnectionString());
        await using var provider = services.BuildServiceProvider();
        await provider.MigrateInkFlowAsync();
        var now = DateTimeOffset.UtcNow;

        var sourceId = Guid.CreateVersion7();
        var sourceBookId = Guid.CreateVersion7();
        var sourceChapterId = Guid.CreateVersion7();
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SourcesDbContext>();
            db.Sources.Add(new SourceRecord { Id = sourceId, Name = "Fixture", BaseUrl = "https://example.test", CapabilitiesJson = "[\"Search\",\"Content\"]", CreatedAtUtc = now, UpdatedAtUtc = now });
            db.SourceBooks.Add(new SourceBookRecord { Id = sourceBookId, SourceId = sourceId, ExternalId = "book-1", Url = "https://example.test/book/1", Title = "测试小说", Author = "测试作者", CreatedAtUtc = now, UpdatedAtUtc = now });
            db.SourceChapters.Add(new SourceChapterRecord { Id = sourceChapterId, SourceBookId = sourceBookId, ExternalId = "chapter-1", Url = "https://example.test/chapter/1", Title = "第一章 开始", Sequence = 100000, CreatedAtUtc = now, UpdatedAtUtc = now });
            await db.SaveChangesAsync();
        }

        var bookId = Guid.CreateVersion7();
        var chapterId = Guid.CreateVersion7();
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LibraryDbContext>();
            db.Books.Add(new BookRecord { Id = bookId, Title = "测试小说", NormalizedTitle = "测试小说", Author = "测试作者", NormalizedAuthor = "测试作者", CreatedAtUtc = now, UpdatedAtUtc = now });
            db.Chapters.Add(new ChapterRecord { Id = chapterId, BookId = bookId, Sequence = 100000, DisplayNumber = 1, Title = "第一章 开始", NormalizedTitle = "第一章开始", CreatedAtUtc = now, UpdatedAtUtc = now });
            db.SourceBookMatches.Add(new SourceBookMatchRecord { Id = Guid.CreateVersion7(), BookId = bookId, SourceBookId = sourceBookId, Score = 95, EvidenceJson = "[]", CreatedAtUtc = now });
            db.ChapterMappings.Add(new ChapterMappingRecord { Id = Guid.CreateVersion7(), ChapterId = chapterId, SourceChapterId = sourceChapterId, Score = 100, EvidenceJson = "[]", CreatedAtUtc = now });
            await db.SaveChangesAsync();
        }

        var blobId = Guid.CreateVersion7();
        var versionId = Guid.CreateVersion7();
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ContentDbContext>();
            db.ContentBlobs.Add(new ContentBlobRecord { Id = blobId, ContentHash = new string('a', 64), InlineContent = "正文", SizeBytes = 6, CreatedAtUtc = now });
            db.ContentVersions.Add(new ContentVersionRecord { Id = versionId, ChapterId = chapterId, SourceChapterId = sourceChapterId, BlobId = blobId, RawHash = new string('b', 64), CanonicalHash = new string('a', 64), QualityScore = 88, EvidenceJson = "[]", CreatedAtUtc = now });
            db.ChapterSelections.Add(new ChapterSelectionRecord { ChapterId = chapterId, ContentVersionId = versionId, SelectedAtUtc = now });
            await db.SaveChangesAsync();
        }

        await using (var scope = provider.CreateAsyncScope())
        {
            var sources = scope.ServiceProvider.GetRequiredService<SourcesDbContext>();
            var library = scope.ServiceProvider.GetRequiredService<LibraryDbContext>();
            var content = scope.ServiceProvider.GetRequiredService<ContentDbContext>();
            Assert.AreEqual("测试小说", (await sources.SourceBooks.SingleAsync()).Title);
            Assert.AreEqual(bookId, (await library.SourceBookMatches.SingleAsync()).BookId);
            Assert.AreEqual(versionId, (await content.ChapterSelections.SingleAsync()).ContentVersionId);
        }
    }
}
