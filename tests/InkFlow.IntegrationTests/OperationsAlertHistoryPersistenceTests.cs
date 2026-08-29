using DotNet.Testcontainers.Images;
using InkFlow.Modules.Operations.Application;
using InkFlow.Modules.Operations.Domain;
using InkFlow.Modules.Operations.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Testcontainers.PostgreSql;

namespace InkFlow.IntegrationTests;

/// <summary>
/// 真实 PostgreSQL 验证告警事件迁移、并发去重、恢复转折、保留清理和追加式约束。
/// 本机无 Docker 时由类初始化明确报告环境阻塞，不能伪造为通过。
/// </summary>
[TestClass]
public sealed class OperationsAlertHistoryPersistenceTests
{
    private static readonly DateTimeOffset T0 =
        new(2026, 8, 1, 12, 0, 0, TimeSpan.Zero);

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
    public async Task Migration_Creates_Operations_Tables_And_Append_Only_History()
    {
        await using var db = CreateDb();
        await db.Database.MigrateAsync().ConfigureAwait(false);

        var tables = await db.Database.SqlQuery<string>(
                $"""SELECT table_name AS "Value" FROM information_schema.tables WHERE table_schema = 'operations'""")
            .ToListAsync()
            .ConfigureAwait(false);
        CollectionAssert.AreEquivalent(
            new[] { "alert_history", "alert_incidents" },
            tables.ToList());

        var observation = CreateObservation("append-only");
        await new EfOperationsAlertHistoryRepository(db)
            .RecordSnapshotAsync(T0, true, [observation], TimeSpan.FromDays(30))
            .ConfigureAwait(false);
        var historyId = await db.AlertHistory
            .Where(history => history.Fingerprint == observation.Fingerprint)
            .Select(history => history.Id)
            .SingleAsync()
            .ConfigureAwait(false);

        Exception? mutationException = null;
        try
        {
            await db.Database.ExecuteSqlInterpolatedAsync(
                    $"""UPDATE "operations"."alert_history" SET "Transition" = {"tampered"} WHERE "Id" = {historyId}""")
                .ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            mutationException = exception;
        }

        Assert.IsNotNull(mutationException);
        StringAssert.Contains(mutationException.ToString(), "append-only");
        Assert.AreEqual(
            OperationsAlertTransitions.Opened,
            await db.AlertHistory
                .Where(history => history.Id == historyId)
                .Select(history => history.Transition)
                .SingleAsync()
                .ConfigureAwait(false));
    }

    [TestMethod]
    public async Task Repeated_Snapshots_Are_Deduplicated_And_Recovery_Is_Recorded()
    {
        await using var db = CreateDb();
        await db.Database.MigrateAsync().ConfigureAwait(false);
        var observation = CreateObservation("recovery");
        var repository = new EfOperationsAlertHistoryRepository(db);

        await repository.RecordSnapshotAsync(
                T0,
                true,
                [observation],
                TimeSpan.FromDays(365))
            .ConfigureAwait(false);
        await repository.RecordSnapshotAsync(
                T0.AddMinutes(1),
                true,
                [observation],
                TimeSpan.FromDays(365))
            .ConfigureAwait(false);
        await repository.RecordSnapshotAsync(
                T0.AddMinutes(2),
                true,
                [],
                TimeSpan.FromDays(365))
            .ConfigureAwait(false);
        await repository.RecordSnapshotAsync(
                T0.AddMinutes(3),
                true,
                [observation],
                TimeSpan.FromDays(365))
            .ConfigureAwait(false);

        var page = await repository.QueryAsync(100).ConfigureAwait(false);
        var entries = page.Entries
            .Where(entry => entry.Fingerprint == observation.Fingerprint)
            .ToList();
        CollectionAssert.AreEqual(
            new[]
            {
                OperationsAlertTransitions.Opened,
                OperationsAlertTransitions.Resolved,
                OperationsAlertTransitions.Opened,
            },
            entries.Select(entry => entry.Transition).ToArray());
        Assert.AreEqual(3, entries[0].OccurrenceCount);
        Assert.AreEqual(2, entries[1].OccurrenceCount);
        Assert.AreEqual(1, entries[2].OccurrenceCount);

        var incident = await db.AlertIncidents
            .AsNoTracking()
            .SingleAsync(candidate => candidate.Fingerprint == observation.Fingerprint)
            .ConfigureAwait(false);
        Assert.AreEqual(OperationsAlertStatuses.Active, incident.Status);
        Assert.AreEqual(3, incident.OccurrenceCount);
    }

    [TestMethod]
    public async Task Concurrent_Same_Snapshot_Creates_One_Incident_And_One_Transition()
    {
        await using var setup = CreateDb();
        await setup.Database.MigrateAsync().ConfigureAwait(false);
        var observation = CreateObservation("concurrent");

        await using var firstDb = CreateDb();
        await using var secondDb = CreateDb();
        await Task.WhenAll(
                new EfOperationsAlertHistoryRepository(firstDb)
                    .RecordSnapshotAsync(
                        T0.AddHours(1),
                        true,
                        [observation],
                        TimeSpan.FromDays(365)),
                new EfOperationsAlertHistoryRepository(secondDb)
                    .RecordSnapshotAsync(
                        T0.AddHours(1),
                        true,
                        [observation],
                        TimeSpan.FromDays(365)))
            .ConfigureAwait(false);

        await using var verify = CreateDb();
        Assert.AreEqual(
            1,
            await verify.AlertHistory
                .CountAsync(history => history.Fingerprint == observation.Fingerprint)
                .ConfigureAwait(false));
        Assert.AreEqual(
            1,
            await verify.AlertIncidents
                .CountAsync(incident => incident.Fingerprint == observation.Fingerprint)
                .ConfigureAwait(false));
    }

    [TestMethod]
    public async Task Incomplete_Snapshot_Does_Not_Resolve_And_Retention_Prunes_Old_History()
    {
        await using var db = CreateDb();
        await db.Database.MigrateAsync().ConfigureAwait(false);
        var observation = CreateObservation("retention");
        var repository = new EfOperationsAlertHistoryRepository(db);

        await repository.RecordSnapshotAsync(
                T0,
                true,
                [observation],
                TimeSpan.FromDays(1))
            .ConfigureAwait(false);
        await repository.RecordSnapshotAsync(
                T0.AddHours(12),
                false,
                [],
                TimeSpan.FromDays(1))
            .ConfigureAwait(false);

        var activeBeforeComplete = await db.AlertIncidents
            .AsNoTracking()
            .SingleAsync(candidate => candidate.Fingerprint == observation.Fingerprint)
            .ConfigureAwait(false);
        Assert.AreEqual(OperationsAlertStatuses.Active, activeBeforeComplete.Status);

        await repository.RecordSnapshotAsync(
                T0.AddDays(2),
                true,
                [],
                TimeSpan.FromDays(1))
            .ConfigureAwait(false);

        var history = await db.AlertHistory
            .AsNoTracking()
            .Where(entry => entry.Fingerprint == observation.Fingerprint)
            .ToListAsync()
            .ConfigureAwait(false);
        Assert.AreEqual(1, history.Count);
        Assert.AreEqual(OperationsAlertTransitions.Resolved, history[0].Transition);
        Assert.AreEqual(
            OperationsAlertStatuses.Resolved,
            (await db.AlertIncidents
                .AsNoTracking()
                .SingleAsync(candidate => candidate.Fingerprint == observation.Fingerprint)
                .ConfigureAwait(false))
            .Status);

        await repository.RecordSnapshotAsync(
                T0.AddDays(4),
                true,
                [],
                TimeSpan.FromDays(1))
            .ConfigureAwait(false);
        Assert.AreEqual(
            0,
            await db.AlertIncidents
                .CountAsync(candidate => candidate.Fingerprint == observation.Fingerprint)
                .ConfigureAwait(false));

        await repository.RecordSnapshotAsync(
                T0.AddDays(5),
                true,
                [observation],
                TimeSpan.FromDays(1))
            .ConfigureAwait(false);
        var recreated = await db.AlertIncidents
            .AsNoTracking()
            .SingleAsync(candidate => candidate.Fingerprint == observation.Fingerprint)
            .ConfigureAwait(false);
        Assert.AreEqual(OperationsAlertStatuses.Active, recreated.Status);
        Assert.AreEqual(1, recreated.OccurrenceCount);
    }

    private static OperationsDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<OperationsDbContext>()
            .UseNpgsql(_container!.GetConnectionString())
            .Options);

    private static OperationsAlertObservation CreateObservation(string suffix) =>
        OperationsAlertObservation.Create(
            "crawler_dead_letters_present",
            "critical",
            "crawler",
            $"dead-letters-{suffix}-{Guid.CreateVersion7():N}");
}
