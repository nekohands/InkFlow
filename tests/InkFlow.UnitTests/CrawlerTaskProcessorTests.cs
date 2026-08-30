using InkFlow.BuildingBlocks.Observability;
using InkFlow.Modules.Crawling.Application;
using InkFlow.Modules.Crawling.Domain;
using InkFlow.Modules.Sources.Domain;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace InkFlow.UnitTests;

[TestClass]
public sealed class CrawlerTaskProcessorTests
{
    private static readonly DateTimeOffset T0 =
        new(2026, 8, 30, 10, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public async Task Successful_Execution_Completes_The_Claimed_Task()
    {
        var task = LeaseTask(maxAttempts: 2);
        var repository = new InMemoryTaskRepository();
        var executor = new RecordingExecutor(CrawlOutcome.Ok());
        var processor = CreateProcessor(repository, executor);

        await processor.ProcessAsync(task);

        Assert.AreEqual(CrawlerTaskStatus.Completed, task.Status);
        Assert.AreEqual(1, executor.CallCount);
        Assert.AreEqual(2, repository.SaveCount);
    }

    [TestMethod]
    public async Task Failed_Execution_Returns_Task_To_Pending_With_A_Schedule()
    {
        var task = LeaseTask(maxAttempts: 2);
        var repository = new InMemoryTaskRepository();
        var executor = new RecordingExecutor(CrawlOutcome.Fail("upstream_unavailable"));
        var processor = CreateProcessor(repository, executor);

        await processor.ProcessAsync(task);

        Assert.AreEqual(CrawlerTaskStatus.Pending, task.Status);
        Assert.IsNotNull(task.ScheduledAt);
        Assert.IsTrue(task.ScheduledAt >= T0);
        Assert.AreEqual(0, repository.DeadLetters.Count);
    }

    [TestMethod]
    public async Task Final_Failure_DeadLetters_The_Task_And_Persists_The_Fact()
    {
        var task = LeaseTask(maxAttempts: 1);
        var repository = new InMemoryTaskRepository();
        var executor = new RecordingExecutor(CrawlOutcome.Fail("upstream_unavailable"));
        var processor = CreateProcessor(repository, executor);

        await processor.ProcessAsync(task);

        Assert.AreEqual(CrawlerTaskStatus.DeadLettered, task.Status);
        Assert.AreEqual(1, repository.DeadLetters.Count);
        Assert.AreEqual(task.Id, repository.DeadLetters[0].TaskId);
        Assert.AreEqual(1, executor.CallCount);
    }

    private static CrawlerTaskProcessor CreateProcessor(
        InMemoryTaskRepository repository,
        RecordingExecutor executor) =>
        new(
            executor,
            repository,
            new FixedTimeProvider(T0.AddSeconds(10)),
            new RetryPolicy { BaseDelay = TimeSpan.FromSeconds(5), MaxDelay = TimeSpan.FromSeconds(5) },
            new CrawlerFailureReporter(
                Array.Empty<ICrawlerFailureSink>(),
                NullLogger<CrawlerFailureReporter>.Instance));

    private static CrawlerTask LeaseTask(int maxAttempts)
    {
        var task = CrawlerTask.Create(
            new CrawlPayload(
                "official-a",
                SourceCapability.Toc,
                new Dictionary<string, string> { ["bookId"] = "book-1" }),
            maxAttempts,
            T0);
        task.Lease(CrawlerTaskExecutionDefaults.Owner, T0, CrawlerTaskExecutionDefaults.LeaseDuration);
        return task;
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class RecordingExecutor(CrawlOutcome outcome) : ICrawlerTaskExecutor
    {
        public int CallCount { get; private set; }

        public Task<CrawlOutcome> ExecuteAsync(
            CrawlerTask task,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(outcome);
        }
    }

    private sealed class InMemoryTaskRepository : ICrawlerTaskRepository
    {
        public int SaveCount { get; private set; }

        public List<DeadLetterTask> DeadLetters { get; } = [];

        public Task AddAsync(CrawlerTask task, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<CrawlerTask?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<CrawlerTask?>(null);

        public Task<CrawlerTask?> TryLeaseAsync(
            DateTimeOffset now,
            string owner,
            TimeSpan leaseDuration,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<CrawlerTask?>(null);

        public Task<CrawlerTask?> TryLeaseAsync(
            Guid taskId,
            DateTimeOffset now,
            string owner,
            TimeSpan leaseDuration,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<CrawlerTask?>(null);

        public Task SaveAsync(CrawlerTask task, CancellationToken cancellationToken = default)
        {
            SaveCount++;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<CrawlerTask>> FindLeasableAsync(
            DateTimeOffset now,
            int limit,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CrawlerTask>>([]);

        public Task AddDeadLetterAsync(
            DeadLetterTask deadLetter,
            CancellationToken cancellationToken = default)
        {
            DeadLetters.Add(deadLetter);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<DeadLetterTask>> ListDeadLettersAsync(
            int limit,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<DeadLetterTask>>(DeadLetters);

        public Task<bool> HasActiveTaskAsync(
            string sourceId,
            SourceCapability capability,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<bool> HasConflictingTaskAsync(
            string sourceId,
            SourceCapability capability,
            string variableName,
            string variableValue,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(false);
    }
}
