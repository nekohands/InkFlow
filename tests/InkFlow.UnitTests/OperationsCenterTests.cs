using System.Text.Json;
using InkFlow.Api;
using InkFlow.Modules.Content.Application;
using InkFlow.Modules.Crawling.Application;
using InkFlow.Modules.Crawling.Domain;
using InkFlow.Modules.Sources.Application;
using InkFlow.Modules.Sources.Domain;

namespace InkFlow.UnitTests;

[TestClass]
public sealed class OperationsCenterTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 28, 18, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public async Task ReadAsync_Combines_Bounded_Operational_Views_Without_Payloads()
    {
        var sourceA = Source.Create("official-a", "Official A", "https://official-a.example", T0);
        var sourceB = Source.Create("official-b", "Official B", "https://official-b.example", T0);
        var health = new FakeSourceHealthOperations();
        var contentHealth = SourceCapabilityHealth.Create(
            sourceA.Id,
            SourceCapability.Content,
            T0);
        contentHealth.RecordFailure("upstream unavailable", T0.AddMinutes(1));
        health.Rows[sourceA.Id] = [contentHealth];

        var deadLetters = new[]
        {
            new DeadLetterTask(
                Guid.Parse("0198f1b3-a0ca-7b01-8a2e-0123456789ab"),
                Guid.Parse("0198f1b3-a0ca-7b02-8a2e-0123456789ab"),
                sourceA.Id,
                "upstream 503",
                3,
                T0.AddMinutes(-3)),
            new DeadLetterTask(
                Guid.Parse("0198f1b3-a0ca-7b03-8a2e-0123456789ab"),
                Guid.Parse("0198f1b3-a0ca-7b04-8a2e-0123456789ab"),
                sourceB.Id,
                "parser rejected response",
                4,
                T0.AddMinutes(-2)),
            new DeadLetterTask(
                Guid.Parse("0198f1b3-a0ca-7b05-8a2e-0123456789ab"),
                Guid.Parse("0198f1b3-a0ca-7b06-8a2e-0123456789ab"),
                sourceB.Id,
                "timeout",
                5,
                T0.AddMinutes(-1)),
        };
        var crawler = new FakeCrawlerTaskRepository(deadLetters);
        var consistency = new FakeConsistencyCheckService(
            new ConsistencyCheckReport(T0, "healthy", 0, 0, false, []));
        var reader = new OperationsCenterReader(
            new FakeSourceRepository([sourceA, sourceB]),
            health,
            crawler,
            consistency,
            new FixedClock(T0));

        var response = await reader.ReadAsync(limit: 2);

        Assert.AreEqual("ready", response.Status);
        Assert.AreEqual(T0, response.GeneratedAt);
        Assert.AreEqual("ready", response.Sources.Status);
        Assert.AreEqual(2, response.Sources.Data!.Count);
        Assert.AreEqual("Content", response.Sources.Data[0].Capabilities.Single().Capability);
        Assert.AreEqual("ready", response.Sources.Data[1].Status);
        Assert.AreEqual("ready", response.Crawler.Status);
        Assert.AreEqual(2, response.Crawler.Data!.ReturnedDeadLetterCount);
        Assert.IsTrue(response.Crawler.Data.HasMoreDeadLetters);
        Assert.AreEqual(3, crawler.RequestedLimit);
        Assert.AreEqual("healthy", response.Consistency.Data!.Status);

        var serialized = JsonSerializer.Serialize(response);
        Assert.IsFalse(serialized.Contains("CredentialReferenceId", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(serialized.Contains("Variables", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(serialized.Contains("sensitive exception", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public async Task ReadAsync_Isolates_Component_Failures_And_Hides_Exception_Details()
    {
        var sourceA = Source.Create("official-a", "Official A", "https://official-a.example", T0);
        var sourceB = Source.Create("official-b", "Official B", "https://official-b.example", T0);
        var health = new FakeSourceHealthOperations { FailingSourceId = sourceB.Id };
        var crawler = new FakeCrawlerTaskRepository([], throwOnRead: true);
        var consistency = new FakeConsistencyCheckService(
            report: null,
            throwOnRead: true);
        var reader = new OperationsCenterReader(
            new FakeSourceRepository([sourceA, sourceB]),
            health,
            crawler,
            consistency,
            new FixedClock(T0));

        var response = await reader.ReadAsync(limit: 0);

        Assert.AreEqual("partial", response.Status);
        Assert.AreEqual("partial", response.Sources.Status);
        Assert.AreEqual("source_health_unavailable", response.Sources.Error);
        Assert.AreEqual("ready", response.Sources.Data![0].Status);
        Assert.AreEqual("unavailable", response.Sources.Data[1].Status);
        Assert.AreEqual("source_health_unavailable", response.Sources.Data[1].Error);
        Assert.AreEqual("unavailable", response.Crawler.Status);
        Assert.AreEqual("crawler_unavailable", response.Crawler.Error);
        Assert.AreEqual("unavailable", response.Consistency.Status);
        Assert.AreEqual("consistency_unavailable", response.Consistency.Error);
        Assert.AreEqual(2, crawler.RequestedLimit);

        var serialized = JsonSerializer.Serialize(response);
        Assert.IsFalse(serialized.Contains("sensitive exception", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public async Task ReadForSourcesAsync_Filters_Source_Health_To_Allowed_Resources()
    {
        var sourceA = Source.Create("official-a", "Official A", "https://official-a.example", T0);
        var sourceB = Source.Create("official-b", "Official B", "https://official-b.example", T0);
        var health = new FakeSourceHealthOperations();
        health.Rows[sourceA.Id] =
        [
            SourceCapabilityHealth.Create(sourceA.Id, SourceCapability.Content, T0),
        ];
        health.Rows[sourceB.Id] =
        [
            SourceCapabilityHealth.Create(sourceB.Id, SourceCapability.Content, T0),
        ];
        var reader = new OperationsCenterReader(
            new FakeSourceRepository([sourceA, sourceB]),
            health,
            new FakeCrawlerTaskRepository([]),
            new FakeConsistencyCheckService(
                new ConsistencyCheckReport(T0, "healthy", 0, 0, false, [])),
            new FixedClock(T0));

        var response = await reader.ReadForSourcesAsync(
            limit: 10,
            new HashSet<string>([sourceB.Id], StringComparer.Ordinal));

        Assert.AreEqual("ready", response.Sources.Status);
        Assert.AreEqual(1, response.Sources.Data!.Count);
        Assert.AreEqual(sourceB.Id, response.Sources.Data[0].SourceId);
        Assert.AreEqual(sourceB.Id, response.Sources.Data[0].Capabilities.Single().SourceId);
    }

    private sealed class FakeSourceRepository(IReadOnlyList<Source> sources) : ISourceRepository
    {
        public Task AddAsync(Source source, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<Source?> GetAsync(
            string sourceId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(sources.SingleOrDefault(source => source.Id == sourceId));

        public Task<IReadOnlyList<Source>> ListAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(sources);

        public Task SaveAsync(Source source, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class FakeSourceHealthOperations : ISourceHealthOperations
    {
        public Dictionary<string, IReadOnlyList<SourceCapabilityHealth>> Rows { get; } = [];
        public string? FailingSourceId { get; init; }

        public Task<SourceCapabilityHealth?> GetAsync(
            string sourceId,
            SourceCapability capability,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(
                Rows.TryGetValue(sourceId, out var rows)
                    ? rows.SingleOrDefault(row => row.Capability == capability)
                    : null);

        public Task<IReadOnlyList<SourceCapabilityHealth>> ListForSourceAsync(
            string sourceId,
            CancellationToken cancellationToken = default)
        {
            if (sourceId == FailingSourceId)
            {
                throw new InvalidOperationException("sensitive exception");
            }

            return Task.FromResult<IReadOnlyList<SourceCapabilityHealth>>(
                Rows.TryGetValue(sourceId, out var rows) ? rows : []);
        }

        public Task<SourceCapabilityHealth> DisableAsync(
            string sourceId,
            SourceCapability capability,
            string reason,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<SourceCapabilityHealth> EnableAsync(
            string sourceId,
            SourceCapability capability,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class FakeCrawlerTaskRepository(
        IReadOnlyList<DeadLetterTask> deadLetters,
        bool throwOnRead = false) : ICrawlerTaskRepository
    {
        public int RequestedLimit { get; private set; }

        public Task AddAsync(CrawlerTask task, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<CrawlerTask?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<CrawlerTask?> TryLeaseAsync(
            DateTimeOffset now,
            string owner,
            TimeSpan leaseDuration,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task SaveAsync(CrawlerTask task, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<CrawlerTask>> FindLeasableAsync(
            DateTimeOffset now,
            int limit,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task AddDeadLetterAsync(
            DeadLetterTask deadLetter,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<DeadLetterTask>> ListDeadLettersAsync(
            int limit,
            CancellationToken cancellationToken = default)
        {
            RequestedLimit = limit;
            if (throwOnRead)
            {
                throw new InvalidOperationException("sensitive exception");
            }

            return Task.FromResult<IReadOnlyList<DeadLetterTask>>(deadLetters.Take(limit).ToList());
        }

        public Task<bool> HasActiveTaskAsync(
            string sourceId,
            SourceCapability capability,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> HasConflictingTaskAsync(
            string sourceId,
            SourceCapability capability,
            string variableName,
            string variableValue,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class FakeConsistencyCheckService(
        ConsistencyCheckReport? report,
        bool throwOnRead = false) : IConsistencyCheckService
    {
        public Task<ConsistencyCheckReport> CheckAsync(
            CancellationToken cancellationToken = default)
        {
            if (throwOnRead)
            {
                throw new InvalidOperationException("sensitive exception");
            }

            return Task.FromResult(report!);
        }
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
