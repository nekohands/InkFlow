using InkFlow.Api;
using InkFlow.Modules.Operations.Application;
using InkFlow.Modules.Operations.Domain;
using InkFlow.Modules.Sources.Domain;
using Microsoft.Extensions.Configuration;

namespace InkFlow.UnitTests;

[TestClass]
public sealed class OperationsAlertTests
{
    private static readonly DateTimeOffset T0 =
        new(2026, 8, 28, 20, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public void Options_Read_And_Validate_Configured_Thresholds()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Operations:Alerts:DeadLetterCountThreshold"] = "3",
                ["Operations:Alerts:UnavailableCapabilityCountThreshold"] = "2",
                ["Operations:Alerts:ConsistencyIssueCountThreshold"] = "5",
                ["Operations:Alerts:MaxReturnedAlerts"] = "20",
                ["Operations:Alerts:HistoryRetentionDays"] = "14",
            })
            .Build();

        var options = OperationsAlertOptions.FromConfiguration(configuration);

        Assert.AreEqual(3, options.DeadLetterCountThreshold);
        Assert.AreEqual(2, options.UnavailableCapabilityCountThreshold);
        Assert.AreEqual(5, options.ConsistencyIssueCountThreshold);
        Assert.AreEqual(20, options.MaxReturnedAlerts);
        Assert.AreEqual(14, options.HistoryRetentionDays);

        var invalid = new OperationsAlertOptions { MaxReturnedAlerts = 0 };
        Assert.ThrowsExactly<InvalidOperationException>(() => invalid.Validate());
    }

    [TestMethod]
    public void Rate_Limit_Store_Health_Tracks_Failure_And_Recovery()
    {
        var health = new RateLimitStoreHealth(new FixedClock(T0));

        Assert.AreEqual(
            RateLimitStoreHealthStatus.Unknown,
            health.GetSnapshot().Status);

        health.RecordFailure();
        health.RecordFailure();
        var unavailable = health.GetSnapshot();
        Assert.AreEqual(RateLimitStoreHealthStatus.Unavailable, unavailable.Status);
        Assert.AreEqual(2, unavailable.ConsecutiveFailures);
        Assert.AreEqual(T0, unavailable.LastFailureAt);

        health.RecordSuccess();
        var recovered = health.GetSnapshot();
        Assert.AreEqual(RateLimitStoreHealthStatus.Healthy, recovered.Status);
        Assert.AreEqual(0, recovered.ConsecutiveFailures);
        Assert.AreEqual(T0, recovered.LastSuccessAt);
    }

    [TestMethod]
    public void Evaluator_Emits_Bounded_Alerts_For_All_Critical_Signals()
    {
        var response = new OperationsCenterResponse(
            T0,
            "ready",
            OperationsSection<IReadOnlyList<OperationsSourceView>>.Ready(
            [
                new OperationsSourceView(
                    "official-a",
                    "Official A",
                    "ready",
                    null,
                    [new SourceHealthResponse(
                        "official-a",
                        "Content",
                        "Unhealthy",
                        3,
                        null,
                        T0.AddMinutes(-1),
                        "upstream secret token",
                        SourceHealthPolicy.AlgorithmVersion,
                        T0,
                        false)]
                ),
            ]),
            OperationsSection<OperationsCrawlerView>.Ready(
                new OperationsCrawlerView(2, false, [])),
            OperationsSection<ConsistencyCheckReport>.Ready(
                new ConsistencyCheckReport(T0, "issues_found", 2, 2, false, [])));

        var alerts = OperationsAlertEvaluator.Evaluate(
            response,
            new RateLimitStoreHealthSnapshot(
                RateLimitStoreHealthStatus.Unavailable,
                2,
                null,
                T0),
            new OperationsAlertOptions());

        CollectionAssert.AreEqual(
            new[]
            {
                "consistency_issues_found",
                "crawler_dead_letters_present",
                "rate_limit_store_unavailable",
                "source_capabilities_unavailable",
            },
            alerts.Select(alert => alert.Code).ToArray());
        Assert.IsFalse(alerts.Any(alert =>
            alert.Message.Contains("secret", StringComparison.OrdinalIgnoreCase)));
        Assert.IsTrue(alerts.All(alert =>
            alert.ResourceId is not null &&
            !alert.ResourceId.Contains("token", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public void Evaluator_Respects_Thresholds_And_Reports_Partial_Snapshots()
    {
        var response = new OperationsCenterResponse(
            T0,
            "partial",
            OperationsSection<IReadOnlyList<OperationsSourceView>>.Partial([], "source_health_unavailable"),
            OperationsSection<OperationsCrawlerView>.Unavailable("crawler_unavailable"),
            OperationsSection<ConsistencyCheckReport>.Ready(
                new ConsistencyCheckReport(T0, "healthy", 0, 0, false, [])));

        var alerts = OperationsAlertEvaluator.Evaluate(
            response,
            new RateLimitStoreHealthSnapshot(
                RateLimitStoreHealthStatus.Healthy,
                0,
                T0,
                null),
            new OperationsAlertOptions
            {
                DeadLetterCountThreshold = 3,
                UnavailableCapabilityCountThreshold = 2,
                ConsistencyIssueCountThreshold = 2,
            });

        CollectionAssert.AreEquivalent(
            new[]
            {
                "operations_snapshot_partial",
                "source_health_snapshot_unavailable",
                "crawler_snapshot_unavailable",
            },
            alerts.Select(alert => alert.Code).ToArray());
    }

    [TestMethod]
    public async Task Reader_Bounds_Returned_Alerts_And_Preserves_Truncation()
    {
        var operations = new FakeOperationsCenterReader(
            new OperationsCenterResponse(
                T0,
                "partial",
                OperationsSection<IReadOnlyList<OperationsSourceView>>.Unavailable("sources_unavailable"),
                OperationsSection<OperationsCrawlerView>.Unavailable("crawler_unavailable"),
                OperationsSection<ConsistencyCheckReport>.Unavailable("consistency_unavailable")));
        var reader = new OperationsAlertReader(
            operations,
            new FixedRateLimitHealthReader(new RateLimitStoreHealthSnapshot(
                RateLimitStoreHealthStatus.Unavailable,
                1,
                null,
                T0)),
            new OperationsAlertOptions { MaxReturnedAlerts = 3 },
            new FixedClock(T0));

        var snapshot = await reader.ReadAsync(1);

        Assert.AreEqual(OperationsCenterReader.MaxLimit, operations.RequestedLimit);
        Assert.AreEqual(T0, snapshot.GeneratedAt);
        Assert.AreEqual(5, snapshot.TotalAlertCount);
        Assert.AreEqual(1, snapshot.ReturnedAlertCount);
        Assert.IsTrue(snapshot.Truncated);
    }

    [TestMethod]
    public async Task Reader_Records_Only_Unfiltered_Complete_Snapshots()
    {
        var operations = new FakeOperationsCenterReader(
            new OperationsCenterResponse(
                T0,
                "ready",
                OperationsSection<IReadOnlyList<OperationsSourceView>>.Ready([]),
                OperationsSection<OperationsCrawlerView>.Ready(
                    new OperationsCrawlerView(0, false, [])),
                OperationsSection<ConsistencyCheckReport>.Ready(
                    new ConsistencyCheckReport(T0, "healthy", 0, 0, false, []))));
        var history = new FakeHistoryRepository();
        var reader = new OperationsAlertReader(
            operations,
            new FixedRateLimitHealthReader(new RateLimitStoreHealthSnapshot(
                RateLimitStoreHealthStatus.Healthy,
                0,
                T0,
                null)),
            new OperationsAlertOptions(),
            new FixedClock(T0),
            history);

        await reader.ReadAsync(10);
        await reader.ReadForSourcesAsync(10, new HashSet<string>(StringComparer.Ordinal)
        {
            "official-a",
        });

        Assert.AreEqual(1, history.RecordedSnapshots);
        Assert.IsTrue(history.LastSnapshotWasComplete);
        Assert.AreEqual(0, history.LastAlerts.Count);
    }

    private sealed class FakeOperationsCenterReader(OperationsCenterResponse response)
        : IOperationsCenterReader
    {
        public int RequestedLimit { get; private set; }

        public Task<OperationsCenterResponse> ReadAsync(
            int limit,
            CancellationToken cancellationToken = default)
        {
            RequestedLimit = limit;
            return Task.FromResult(response);
        }

        public Task<OperationsCenterResponse> ReadForSourcesAsync(
            int limit,
            IReadOnlySet<string> allowedSourceIds,
            CancellationToken cancellationToken = default)
        {
            RequestedLimit = limit;
            return Task.FromResult(response);
        }
    }

    private sealed class FixedRateLimitHealthReader(RateLimitStoreHealthSnapshot snapshot)
        : IRateLimitStoreHealthReader
    {
        public RateLimitStoreHealthSnapshot GetSnapshot() => snapshot;
    }

    private sealed class FakeHistoryRepository : IOperationsAlertHistoryRepository
    {
        public int RecordedSnapshots { get; private set; }

        public bool LastSnapshotWasComplete { get; private set; }

        public IReadOnlyCollection<OperationsAlertObservation> LastAlerts { get; private set; } = [];

        public Task RecordSnapshotAsync(
            DateTimeOffset observedAt,
            bool isCompleteSnapshot,
            IReadOnlyCollection<OperationsAlertObservation> activeAlerts,
            TimeSpan retention,
            CancellationToken cancellationToken = default)
        {
            RecordedSnapshots++;
            LastSnapshotWasComplete = isCompleteSnapshot;
            LastAlerts = activeAlerts;
            return Task.CompletedTask;
        }

        public Task<OperationsAlertHistoryPage> QueryAsync(
            int limit,
            OperationsAlertHistoryCursor? before = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new OperationsAlertHistoryPage([], null));
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
