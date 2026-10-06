using DotNet.Testcontainers.Images;
using InkFlow.Modules.Library.Application;
using InkFlow.Modules.Library.Domain;
using InkFlow.Modules.Library.Infrastructure.Persistence;
using InkFlow.Modules.Sources.Application;
using InkFlow.Modules.Sources.Domain;
using InkFlow.Modules.Sources.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace InkFlow.IntegrationTests;

/// <summary>
/// 真实 PostgreSQL 上的并发匹配回归：归一化同名同作者的多来源并发匹配必须只产生
/// 一个正典身份（BookId 稳定不变量的并发面）。本机无 Docker 时由类初始化阻塞。
/// </summary>
[TestClass]
public sealed class CanonicalMatchConcurrencyTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);
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
    public async Task Concurrent_Matches_For_Same_Book_Identity_Create_One_Canonical_Book()
    {
        await using (var seedDb = CreateSourcesDb())
        {
            var sourceBooks = new EfSourceBookRepository(seedDb);
            // 空白差异必须走同一归一化身份："同一本书/烽火戏诸侯"。
            await sourceBooks.AddAsync(SourceBook.Create(
                "official-a", "book-a", "同一本书", "烽火戏诸侯", T0)).ConfigureAwait(false);
            await sourceBooks.AddAsync(SourceBook.Create(
                "official-b", "book-b", "同 一本书", "烽火 戏诸侯", T0)).ConfigureAwait(false);
        }

        const int parallelism = 8;
        var tasks = Enumerable.Range(0, parallelism)
            .Select(index => Task.Run(() => MatchAsync(index)))
            .ToArray();
        var results = await Task.WhenAll(tasks).ConfigureAwait(false);

        Assert.IsTrue(results.All(result => result.IsSuccess), "并发匹配全部成功");

        await using var verifyDb = CreateLibraryDb();
        var books = await verifyDb.Books.AsNoTracking().ToListAsync().ConfigureAwait(false);
        Assert.AreEqual(1, books.Count, "并发匹配不得创建重复正典身份");
        Assert.IsTrue(results.All(result => result.Book!.Id == books.Single().Id));

        var candidates = await verifyDb.MatchCandidates.AsNoTracking().ToListAsync().ConfigureAwait(false);
        Assert.AreEqual(2, candidates.Count, "每个来源书至多一个已确认候选");
        Assert.AreEqual(1, results.Count(result => result.NewlyCreated), "并发下至多一次真正创建");
        Assert.IsTrue(candidates.All(candidate =>
            candidate.Status == (int)MatchCandidateStatus.Confirmed));
    }

    private static async Task<MatchOutcome> MatchAsync(int index)
    {
        var source = index % 2 == 0 ? "official-a" : "official-b";
        var externalBookId = source == "official-a" ? "book-a" : "book-b";

        // 每个并发请求使用独立的 DbContext/仓储/服务实例，等价于独立请求作用域；
        // 与 Api 组合根一致：来源书在 sources schema，正典身份在 library schema。
        await using var sourcesDb = CreateSourcesDb();
        await using var libraryDb = CreateLibraryDb();
        var matching = new CanonicalBookMatchingService(
            new EfSourceBookRepository(sourcesDb),
            new EfCanonicalBookRepository(libraryDb),
            new EfMatchCandidateRepository(libraryDb));
        return await matching.CreateOrMatchAsync(source, externalBookId).ConfigureAwait(false);
    }

    private static SourcesDbContext CreateSourcesDb()
    {
        var options = new DbContextOptionsBuilder<SourcesDbContext>()
            .UseNpgsql(_container!.GetConnectionString())
            .Options;
        var db = new SourcesDbContext(options);
        db.Database.Migrate();
        return db;
    }

    private static LibraryDbContext CreateLibraryDb()
    {
        var options = new DbContextOptionsBuilder<LibraryDbContext>()
            .UseNpgsql(_container!.GetConnectionString())
            .Options;
        var db = new LibraryDbContext(options);
        db.Database.Migrate();
        return db;
    }
}
