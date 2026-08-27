using DotNet.Testcontainers.Images;
using InkFlow.Api;
using InkFlow.Modules.Content.Infrastructure.Persistence;
using InkFlow.Modules.Crawling.Infrastructure.Persistence;
using InkFlow.Modules.Library.Infrastructure.Persistence;
using InkFlow.Modules.Sources.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Testcontainers.PostgreSql;

namespace InkFlow.IntegrationTests;

/// <summary>
/// 真实 PostgreSQL 上验证跨 schema 一致性快照 Adapter：只读投影能发现跨模块孤儿引用。
/// 本机无 Docker 时由类初始化明确阻塞，不能把环境缺失伪装成通过。
/// </summary>
[TestClass]
public sealed class ConsistencyCheckIntegrationTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 28, 17, 0, 0, TimeSpan.Zero);

    private static PostgreSqlContainer? _container;

    [ClassInitialize]
    public static async Task StartContainerAsync(TestContext _)
    {
        _container = new PostgreSqlBuilder(new DockerImage("postgres:18-alpine"))
            .Build();
        await _container.StartAsync().ConfigureAwait(false);
    }

    [ClassCleanup]
    public static async Task StopContainerAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync().ConfigureAwait(false);
        }
    }

    [TestMethod]
    public async Task Ef_snapshot_reader_detects_orphan_content_version_without_loading_body()
    {
        await using var library = CreateLibraryContext();
        await using var sources = CreateSourcesContext();
        await using var content = CreateContentContext();
        await using var crawling = CreateCrawlingContext();

        var sourceId = "consistency-source";
        sources.Sources.Add(new SourceEntity
        {
            Id = sourceId,
            DisplayName = "Consistency Source",
            BaseUrl = "https://example.com",
            CreatedAt = T0,
            UpdatedAt = T0,
        });
        await sources.SaveChangesAsync().ConfigureAwait(false);

        var bookId = Guid.NewGuid();
        library.Books.Add(new CanonicalBookEntity
        {
            Id = bookId,
            Title = "一致性测试书",
            Author = "测试作者",
            CreatedAt = T0,
            UpdatedAt = T0,
        });
        await library.SaveChangesAsync().ConfigureAwait(false);

        var missingChapterId = Guid.NewGuid();
        content.Versions.Add(new ContentVersionEntity
        {
            Id = Guid.NewGuid(),
            CanonicalBookId = bookId,
            CanonicalChapterId = missingChapterId,
            SourceId = sourceId,
            CanonicalHash = "hash-consistency",
            CanonicalText = "这段正文不应被快照读取",
            ParagraphCount = 1,
            QualityScore = 80,
            QualityAlgorithmVersion = "quality-v1",
            QualityEvidence = "fixture",
            IsCurrent = true,
            CreatedAt = T0,
        });
        await content.SaveChangesAsync().ConfigureAwait(false);

        var snapshot = await new EfConsistencySnapshotReader(library, sources, content, crawling)
            .ReadAsync()
            .ConfigureAwait(false);
        Assert.AreEqual(12, snapshot.ContentVersions.Single().CanonicalTextLength);

        var service = new ConsistencyCheckService(
            new FixedSnapshotReader(snapshot),
            new FixedClock(T0));

        var report = await service.CheckAsync().ConfigureAwait(false);

        Assert.IsTrue(report.Issues.Any(issue =>
            issue.Code == "content_version_canonical_chapter_missing"));
        Assert.IsFalse(report.Issues.Any(issue => issue.Message.Contains("这段正文")));
    }

    private static LibraryDbContext CreateLibraryContext()
    {
        var options = new DbContextOptionsBuilder<LibraryDbContext>()
            .UseNpgsql(_container!.GetConnectionString())
            .Options;
        var db = new LibraryDbContext(options);
        db.Database.Migrate();
        return db;
    }

    private static SourcesDbContext CreateSourcesContext()
    {
        var options = new DbContextOptionsBuilder<SourcesDbContext>()
            .UseNpgsql(_container!.GetConnectionString())
            .Options;
        var db = new SourcesDbContext(options);
        db.Database.Migrate();
        return db;
    }

    private static ContentDbContext CreateContentContext()
    {
        var options = new DbContextOptionsBuilder<ContentDbContext>()
            .UseNpgsql(_container!.GetConnectionString())
            .Options;
        var db = new ContentDbContext(options);
        db.Database.Migrate();
        return db;
    }

    private static CrawlingDbContext CreateCrawlingContext()
    {
        var options = new DbContextOptionsBuilder<CrawlingDbContext>()
            .UseNpgsql(_container!.GetConnectionString())
            .Options;
        var db = new CrawlingDbContext(options);
        db.Database.Migrate();
        return db;
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class FixedSnapshotReader(ConsistencySnapshot snapshot) : IConsistencySnapshotReader
    {
        public Task<ConsistencySnapshot> ReadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(snapshot);
    }
}
