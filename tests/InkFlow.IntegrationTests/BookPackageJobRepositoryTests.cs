using InkFlow.Modules.Content.Domain;
using InkFlow.Modules.Content.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Testcontainers.PostgreSql;
using DotNet.Testcontainers.Images;

namespace InkFlow.IntegrationTests;

/// <summary>书籍包任务租约的 PostgreSQL 持久化与崩溃恢复边界测试。</summary>
[TestClass]
public sealed class BookPackageJobRepositoryTests
{
    private static readonly DateTimeOffset T0 =
        new(2026, 8, 31, 10, 0, 0, TimeSpan.Zero);
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
    public async Task Expired_Lease_Consumes_Attempt_And_Reaches_Failed_At_Budget()
    {
        await using var db = CreateDb();
        await db.Database.MigrateAsync().ConfigureAwait(false);
        var repository = new EfBookPackageJobRepository(db);
        var job = BookPackageJob.Create(
            Guid.CreateVersion7(),
            BookPackageFormat.Zip,
            T0,
            T0.AddDays(1),
            maxAttempts: 1);

        await repository.AddAsync(job).ConfigureAwait(false);
        var first = await repository
            .TryLeaseAsync(T0, "worker-a", TimeSpan.FromMinutes(1))
            .ConfigureAwait(false);
        Assert.IsNotNull(first);
        Assert.AreEqual(1, first!.AttemptCount);

        var second = await repository
            .TryLeaseAsync(T0.AddMinutes(2), "worker-b", TimeSpan.FromMinutes(1))
            .ConfigureAwait(false);
        Assert.IsNull(second);

        var persisted = await repository.GetAsync(job.Id).ConfigureAwait(false);
        Assert.IsNotNull(persisted);
        Assert.AreEqual(BookPackageJobStatus.Failed, persisted!.Status);
        Assert.AreEqual("package lease expired after maximum attempts.", persisted.FailureReason);
    }

    [TestMethod]
    public async Task Expired_Lease_Is_Reclaimed_As_The_Next_Attempt()
    {
        await using var db = CreateDb();
        await db.Database.MigrateAsync().ConfigureAwait(false);
        var repository = new EfBookPackageJobRepository(db);
        var job = BookPackageJob.Create(
            Guid.CreateVersion7(),
            BookPackageFormat.Epub,
            T0,
            T0.AddDays(1),
            maxAttempts: 2);

        await repository.AddAsync(job).ConfigureAwait(false);
        _ = await repository
            .TryLeaseAsync(T0, "worker-a", TimeSpan.FromMinutes(1))
            .ConfigureAwait(false);

        var reclaimed = await repository
            .TryLeaseAsync(T0.AddMinutes(2), "worker-b", TimeSpan.FromMinutes(1))
            .ConfigureAwait(false);

        Assert.IsNotNull(reclaimed);
        Assert.AreEqual(BookPackageJobStatus.Running, reclaimed!.Status);
        Assert.AreEqual(2, reclaimed.AttemptCount);
        Assert.AreEqual("worker-b", reclaimed.LeaseOwner);
    }

    private static ContentDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ContentDbContext>()
            .UseNpgsql(_container!.GetConnectionString())
            .Options;
        return new ContentDbContext(options);
    }
}
