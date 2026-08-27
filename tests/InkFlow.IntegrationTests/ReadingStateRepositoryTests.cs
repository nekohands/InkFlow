using DotNet.Testcontainers.Images;
using InkFlow.Modules.Reading.Domain;
using InkFlow.Modules.Reading.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace InkFlow.IntegrationTests;

/// <summary>真实 PostgreSQL 上验证 Reading schema、用户隔离和并发安全的幂等 upsert。</summary>
[TestClass]
public sealed class ReadingStateRepositoryTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 28, 17, 0, 0, TimeSpan.Zero);
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
    public async Task Migration_Creates_Reading_Tables_And_No_Pending_Migrations()
    {
        await using var db = CreateDb();
        await db.Database.MigrateAsync().ConfigureAwait(false);

        var pending = await db.Database.GetPendingMigrationsAsync().ConfigureAwait(false);
        Assert.IsFalse(pending.Any());

        var tables = await db.Database.SqlQuery<string>(
                $"SELECT table_name AS \"Value\" FROM information_schema.tables WHERE table_schema = 'reading'")
            .ToListAsync()
            .ConfigureAwait(false);
        CollectionAssert.AreEquivalent(
            new[] { "shelf_entries", "progress", "history", "preferences" },
            tables.ToList());
    }

    [TestMethod]
    public async Task State_Roundtrips_Per_User_And_Stale_Writes_Do_Not_Replace_New_Progress()
    {
        await using var db = CreateDb();
        var repository = new EfReadingStateRepository(db);
        var userA = Guid.CreateVersion7();
        var userB = Guid.CreateVersion7();
        var bookId = Guid.CreateVersion7();
        var firstChapterId = Guid.CreateVersion7();
        var secondChapterId = Guid.CreateVersion7();

        await repository.UpsertShelfAsync(
            BookshelfEntry.Create(userA, bookId, ShelfStatus.Reading, T0));
        await repository.UpsertShelfAsync(
            BookshelfEntry.Create(userB, bookId, ShelfStatus.WantToRead, T0));

        var firstProgress = ReadingProgress.Create(
            userA, bookId, firstChapterId, paragraphIndex: 2, progressPercent: 20, now: T0);
        var firstHistory = ReadingHistoryEntry.Create(userA, bookId, firstChapterId, T0);
        await repository.SaveProgressAsync(firstProgress, firstHistory);

        var latestProgress = ReadingProgress.Create(
            userA, bookId, secondChapterId, paragraphIndex: 5, progressPercent: 70, now: T0.AddMinutes(1));
        var latestHistory = ReadingHistoryEntry.Create(
            userA, bookId, secondChapterId, T0.AddMinutes(1));
        await repository.SaveProgressAsync(latestProgress, latestHistory);

        // 旧请求晚到时，更新时间条件保证不会回退当前阅读位置。
        await repository.SaveProgressAsync(firstProgress, firstHistory);

        var loadedProgress = await repository.GetProgressAsync(userA, bookId);
        Assert.IsNotNull(loadedProgress);
        Assert.AreEqual(secondChapterId, loadedProgress!.CanonicalChapterId);
        Assert.AreEqual(70, loadedProgress.ProgressPercent);

        var userAHistory = await repository.ListHistoryAsync(userA, 100);
        Assert.AreEqual(2, userAHistory.Count);
        Assert.AreEqual(0, (await repository.ListHistoryAsync(userB, 100)).Count);
        Assert.IsNotNull(await repository.GetShelfEntryAsync(userB, bookId));
        Assert.IsNull(await repository.GetProgressAsync(userB, bookId));
    }

    [TestMethod]
    public async Task Preferences_And_Shelf_Delete_Are_Idempotent()
    {
        await using var db = CreateDb();
        var repository = new EfReadingStateRepository(db);
        var userId = Guid.CreateVersion7();
        var bookId = Guid.CreateVersion7();

        var preference = ReaderPreference.CreateDefault(userId, T0);
        preference.Update(120, 200, ReaderTheme.Sepia, T0.AddMinutes(1));
        await repository.UpsertPreferencesAsync(preference);
        await repository.UpsertPreferencesAsync(preference);

        var loadedPreference = await repository.GetPreferencesAsync(userId);
        Assert.IsNotNull(loadedPreference);
        Assert.AreEqual(ReaderTheme.Sepia, loadedPreference!.Theme);

        await repository.UpsertShelfAsync(
            BookshelfEntry.Create(userId, bookId, ShelfStatus.Reading, T0));
        await repository.RemoveShelfAsync(userId, bookId);
        await repository.RemoveShelfAsync(userId, bookId);
        Assert.IsNull(await repository.GetShelfEntryAsync(userId, bookId));
    }

    private static ReadingDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ReadingDbContext>()
            .UseNpgsql(_container!.GetConnectionString())
            .Options;
        var db = new ReadingDbContext(options);
        db.Database.Migrate();
        return db;
    }
}
