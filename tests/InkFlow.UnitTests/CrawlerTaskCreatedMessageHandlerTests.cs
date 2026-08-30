using InkFlow.BuildingBlocks.Messaging;
using InkFlow.Modules.Crawling.Application;
using InkFlow.Modules.Crawling.Domain;
using InkFlow.Modules.Sources.Domain;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace InkFlow.UnitTests;

[TestClass]
public sealed class CrawlerTaskCreatedMessageHandlerTests
{
    private static readonly DateTimeOffset T0 =
        new(2026, 8, 30, 10, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public async Task Created_Event_Atomically_Claims_And_Processes_The_Task()
    {
        var task = NewTask();
        var repository = new InMemoryTaskRepository(task);
        var processor = new RecordingTaskProcessor();
        var handler = new CrawlerTaskCreatedMessageHandler(
            repository,
            processor,
            new FixedTimeProvider(T0.AddSeconds(1)));

        await handler.HandleAsync(CrawlerIntegrationMessages.TaskCreated(task));

        Assert.AreEqual(task.Id, repository.ClaimedTaskId);
        Assert.AreSame(task, processor.ProcessedTask);
    }

    [TestMethod]
    public async Task Replayed_Event_Does_Not_Reprocess_A_Terminal_Task()
    {
        var task = NewTask();
        task.Lease("worker", T0, TimeSpan.FromMinutes(2));
        task.MarkRunning(T0);
        task.Complete(T0.AddSeconds(1));
        var repository = new InMemoryTaskRepository(task);
        var processor = new RecordingTaskProcessor();
        var handler = new CrawlerTaskCreatedMessageHandler(
            repository,
            processor,
            new FixedTimeProvider(T0.AddSeconds(2)));

        await handler.HandleAsync(CrawlerIntegrationMessages.TaskCreated(task));

        Assert.IsNull(repository.ClaimedTaskId);
        Assert.IsNull(processor.ProcessedTask);
    }

    [TestMethod]
    public async Task Created_Event_With_Mismatched_Authoritative_Task_Is_Rejected()
    {
        var task = NewTask();
        var repository = new InMemoryTaskRepository(task);
        var processor = new RecordingTaskProcessor();
        var handler = new CrawlerTaskCreatedMessageHandler(
            repository,
            processor,
            new FixedTimeProvider(T0));
        var mismatched = IntegrationMessage.Create(
            CrawlerIntegrationMessages.TaskCreatedType,
            $$"""{"taskId":"{{task.Id}}","sourceId":"other-source","capability":"Toc","status":"Pending","attemptCount":0,"createdAt":"{{task.CreatedAt:O}}"}""",
            T0,
            id: task.Id);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => handler.HandleAsync(mismatched));

        Assert.IsNull(repository.ClaimedTaskId);
        Assert.IsNull(processor.ProcessedTask);
    }

    private static CrawlerTask NewTask() =>
        CrawlerTask.Create(
            new CrawlPayload(
                "official-a",
                SourceCapability.Toc,
                new Dictionary<string, string> { ["bookId"] = "book-1" }),
            createdAt: T0);

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class RecordingTaskProcessor : ICrawlerTaskProcessor
    {
        public CrawlerTask? ProcessedTask { get; private set; }

        public Task ProcessAsync(CrawlerTask task, CancellationToken cancellationToken = default)
        {
            ProcessedTask = task;
            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryTaskRepository(CrawlerTask task) : ICrawlerTaskRepository
    {
        public Guid? ClaimedTaskId { get; private set; }

        public Task AddAsync(CrawlerTask task, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<CrawlerTask?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<CrawlerTask?>(id == task.Id ? task : null);

        public Task<CrawlerTask?> TryLeaseAsync(
            DateTimeOffset now,
            string owner,
            TimeSpan leaseDuration,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<CrawlerTask?> TryLeaseAsync(
            Guid taskId,
            DateTimeOffset now,
            string owner,
            TimeSpan leaseDuration,
            CancellationToken cancellationToken = default)
        {
            if (taskId != task.Id || !task.IsLeasable(now))
            {
                return Task.FromResult<CrawlerTask?>(null);
            }

            task.Lease(owner, now, leaseDuration);
            ClaimedTaskId = task.Id;
            return Task.FromResult<CrawlerTask?>(task);
        }

        public Task SaveAsync(CrawlerTask task, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<CrawlerTask>> FindLeasableAsync(
            DateTimeOffset now,
            int limit,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CrawlerTask>>([]);

        public Task AddDeadLetterAsync(
            DeadLetterTask deadLetter,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<DeadLetterTask>> ListDeadLettersAsync(
            int limit,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<DeadLetterTask>>([]);

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
