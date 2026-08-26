using InkFlow.Modules.Library.Application;
using InkFlow.Modules.Library.Domain;
using InkFlow.Modules.Library.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Testcontainers.PostgreSql;
using DotNet.Testcontainers.Images;

namespace InkFlow.IntegrationTests;

/// <summary>匹配候选仓储集成测试：真实 PostgreSQL 18 上验证迁移与唯一约束。</summary>
[TestClass]
public sealed class MatchCandidateRepositoryTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 26, 19, 0, 0, TimeSpan.Zero);

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

    private static (EfMatchCandidateRepository Repo, LibraryDbContext Db) CreateRepository()
    {
        var options = new DbContextOptionsBuilder<LibraryDbContext>()
            .UseNpgsql(_container!.GetConnectionString())
            .Options;

        var db = new LibraryDbContext(options);
        db.Database.Migrate();
        return (new EfMatchCandidateRepository(db), db);
    }

    [TestMethod]
    public async Task Candidate_Roundtrips_With_Status_And_Identity()
    {
        var (repo, _) = CreateRepository();
        var candidate = MatchCandidate.Confirm(Guid.NewGuid(), "example-source", "10001", T0);
        await repo.AddAsync(candidate).ConfigureAwait(false);

        var loaded = await repo.FindForSourceBookAsync("example-source", "10001").ConfigureAwait(false);
        Assert.IsNotNull(loaded);
        Assert.AreEqual(MatchCandidateStatus.Confirmed, loaded.Status);
        Assert.AreEqual(candidate.CanonicalBookId, loaded.CanonicalBookId);
        Assert.IsNull(await repo.FindForSourceBookAsync("other-source", "10001").ConfigureAwait(false));
    }
}
