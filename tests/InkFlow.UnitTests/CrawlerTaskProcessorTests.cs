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

    [TestMethod]
    public async Task Rejected_Atomic_Start_Does_Not_Invoke_Executor()
    {
        var task = LeaseTask(maxAttempts: 2);
        var repository = new InMemoryTaskRepository { RejectStart = true };
        var executor = new RecordingExecutor(CrawlOutcome.Ok());
        var processor = CreateProcessor(repository, executor);

        await processor.ProcessAsync(task);

        Assert.AreEqual(CrawlerTaskStatus.Leased, task.Status);
        Assert.AreEqual(0, executor.CallCount);
        Assert.AreEqual(0, repository.SaveCount);
    }

    [TestMethod]
    public async Task Rejected_Atomic_Start_Does_Not_Advance_Pending_Collection_Run()
    {
        var run = CollectionRun.Create(
            "official-a",
            "book-1",
            "https://books.example.com/book-1",
            T0);
        var task = CrawlerTask.Create(
            new CrawlPayload(
                "official-a",
                SourceCapability.Toc,
                new Dictionary<string, string> { ["bookId"] = "book-1" },
                RunId: run.Id),
            maxAttempts: 2,
            T0);
        task.Lease(CrawlerTaskExecutionDefaults.Owner, T0, CrawlerTaskExecutionDefaults.LeaseDuration);

        var taskRepository = new InMemoryTaskRepository { RejectStart = true };
        var runRepository = new InMemoryCollectionRunRepository(run);
        var collectionRuns = new CollectionRunService(
            urlResolver: null!,
            runs: runRepository,
            clock: new FixedTimeProvider(T0.AddSeconds(10)));
        var executor = new RecordingExecutor(CrawlOutcome.Ok());
        var processor = CreateProcessor(taskRepository, executor, collectionRuns);

        await processor.ProcessAsync(task);

        Assert.AreEqual(CollectionRunStatus.Pending, run.Status);
        Assert.AreEqual(0, executor.CallCount);
        Assert.AreEqual(0, taskRepository.SaveCount);
    }

    private static CrawlerTaskProcessor CreateProcessor(
        InMemoryTaskRepository repository,
        RecordingExecutor executor,
        CollectionRunService? collectionRuns = null) =>
        new(
            executor,
            repository,
            new FixedTimeProvider(T0.AddSeconds(10)),
            new RetryPolicy { BaseDelay = TimeSpan.FromSeconds(5), MaxDelay = TimeSpan.FromSeconds(5) },
            new CrawlerFailureReporter(
                Array.Empty<ICrawlerFailureSink>(),
                NullLogger<CrawlerFailureReporter>.Instance),
            collectionRuns);

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

        public bool RejectStart { get; init; }

        public List<DeadLetterTask> DeadLetters { get; } = [];

        public Task AddAsync(CrawlerTask task, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> TryMarkRunningAsync(
            CrawlerTask task,
            DateTimeOffset now,
            CancellationToken cancellationToken = default)
        {
            if (RejectStart)
            {
                return Task.FromResult(false);
            }

            task.MarkRunning(now);
            SaveCount++;
            return Task.FromResult(true);
        }

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

    private sealed class InMemoryCollectionRunRepository(CollectionRun run) : ICollectionRunRepository
    {
        public Task AddAsync(CollectionRun value, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> TryAddAsync(CollectionRun value, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> TryAddWithInitialTaskAsync(
            CollectionRun value,
            CrawlerTask task,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<CollectionRun?> GetAsync(
            Guid id,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<CollectionRun?>(id == run.Id ? run : null);

        public Task<CollectionRun?> MutateAsync(
            Guid id,
            Action<CollectionRun> mutation,
            DateTimeOffset now,
            CancellationToken cancellationToken = default)
        {
            if (id == run.Id)
            {
                mutation(run);
            }

            return Task.FromResult<CollectionRun?>(id == run.Id ? run : null);
        }

        public Task<CollectionRun?> ReconcileAsync(
            Guid id,
            DateTimeOffset now,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<CollectionRun?>(id == run.Id ? run : null);

        public Task<CollectionRun?> FindActiveAsync(
            string sourceId,
            string externalBookId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<CollectionRun>> ListAsync(
            int limit,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task SaveAsync(CollectionRun value, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<CollectionRunTaskProgress> GetTaskProgressAsync(
            Guid runId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new CollectionRunTaskProgress(1, 1, 0, 0, 0, 0, 0));
    }
}
