using InkFlow.Modules.Content.Domain;
using InkFlow.Modules.Content.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Testcontainers.PostgreSql;
using DotNet.Testcontainers.Images;

namespace InkFlow.IntegrationTests;

/// <summary>内容版本当前选择的持久化边界测试。</summary>
[TestClass]
public sealed class ContentVersionRepositoryTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 30, 10, 0, 0, TimeSpan.Zero);
    private static PostgreSqlContainer? _container;

    [ClassInitialize]
    public static async Task StartContainerAsync(TestContext _)
    {
        _container = new PostgreSqlBuilder(new DockerImage("postgres:18-alpine")).Build();
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
    public async Task SetCurrentAsync_rejects_version_from_another_chapter_without_clearing_current()
    {
        await using var db = CreateDb();
        await db.Database.MigrateAsync().ConfigureAwait(false);
        var repository = new EfContentVersionRepository(db);
        var chapterId = Guid.CreateVersion7();
        var otherChapterId = Guid.CreateVersion7();
        var bookId = Guid.CreateVersion7();
        var otherBookId = Guid.CreateVersion7();
        var current = NewVersion(bookId, chapterId, "source-a", "当前正文");
        var otherChapterVersion = NewVersion(otherBookId, otherChapterId, "source-b", "其他章节正文");

        await repository.AddAsync(current).ConfigureAwait(false);
        await repository.AddAsync(otherChapterVersion).ConfigureAwait(false);
        await repository.SetCurrentAsync(chapterId, current.Id).ConfigureAwait(false);

        var rejected = false;
        try
        {
            await repository.SetCurrentAsync(chapterId, otherChapterVersion.Id)
                .ConfigureAwait(false);
        }
        catch (InvalidOperationException)
        {
            rejected = true;
        }

        Assert.IsTrue(rejected, "a version from another chapter must be rejected");

        var loaded = await repository.GetCurrentForChapterAsync(chapterId).ConfigureAwait(false);
        Assert.IsNotNull(loaded);
        Assert.AreEqual(current.Id, loaded!.Id);

        var other = await db.Versions
            .AsNoTracking()
            .SingleAsync(version => version.Id == otherChapterVersion.Id)
            .ConfigureAwait(false);
        Assert.IsFalse(other.IsCurrent);
    }

    [TestMethod]
    public async Task SetCurrentAsync_switches_only_one_current_version_within_the_chapter()
    {
        await using var db = CreateDb();
        await db.Database.MigrateAsync().ConfigureAwait(false);
        var repository = new EfContentVersionRepository(db);
        var chapterId = Guid.CreateVersion7();
        var bookId = Guid.CreateVersion7();
        var first = NewVersion(bookId, chapterId, "source-a", "第一份正文");
        var second = NewVersion(bookId, chapterId, "source-b", "第二份正文");

        await repository.AddAsync(first).ConfigureAwait(false);
        await repository.AddAsync(second).ConfigureAwait(false);
        await repository.SetCurrentAsync(chapterId, first.Id).ConfigureAwait(false);
        await repository.SetCurrentAsync(chapterId, second.Id).ConfigureAwait(false);

        var current = await repository.GetCurrentForChapterAsync(chapterId).ConfigureAwait(false);
        Assert.IsNotNull(current);
        Assert.AreEqual(second.Id, current!.Id);

        var currentCount = await db.Versions
            .AsNoTracking()
            .CountAsync(version => version.CanonicalChapterId == chapterId && version.IsCurrent)
            .ConfigureAwait(false);
        Assert.AreEqual(1, currentCount);
    }

    private static ContentDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ContentDbContext>()
            .UseNpgsql(_container!.GetConnectionString())
            .Options;
        return new ContentDbContext(options);
    }

    private static ContentVersion NewVersion(
        Guid bookId, Guid chapterId, string sourceId, string content) =>
        ContentVersion.Create(
            bookId,
            chapterId,
            sourceId,
            ContentNormalizer.Normalize($"<p>{content}</p>"),
            T0);
}
