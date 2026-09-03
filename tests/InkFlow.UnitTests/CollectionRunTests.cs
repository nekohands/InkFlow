using InkFlow.Modules.Crawling.Application;
using InkFlow.Modules.Crawling.Domain;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace InkFlow.UnitTests;

[TestClass]
public sealed class CollectionRunTests
{
    private static readonly DateTimeOffset T0 =
        new(2026, 8, 31, 10, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public void Pause_And_Resume_Preserve_Run_And_Resume_To_Pending()
    {
        var run = CreateRun();

        run.MarkWorkStarted(T0.AddSeconds(1));
        run.Pause(T0.AddSeconds(2));
        Assert.AreEqual(CollectionRunStatus.Paused, run.Status);

        run.Resume(T0.AddSeconds(3));

        Assert.AreEqual(CollectionRunStatus.Pending, run.Status);
        Assert.AreEqual("book/42", run.ExternalBookId);
    }

    [TestMethod]
    public void Repeating_The_Same_Control_Is_A_Successful_NoOp()
    {
        var run = CreateRun();

        run.Pause(T0.AddSeconds(1));
        run.Pause(T0.AddSeconds(2));
        run.Resume(T0.AddSeconds(3));
        run.Resume(T0.AddSeconds(4));

        Assert.AreEqual(CollectionRunStatus.Pending, run.Status);

        run.RequestStop(T0.AddSeconds(5));
        run.RequestStop(T0.AddSeconds(6));
        run.Reconcile(new CollectionRunTaskProgress(0, 0, 0, 0, 0, 0, 0), T0.AddSeconds(7));
        run.RequestStop(T0.AddSeconds(8));

        Assert.AreEqual(CollectionRunStatus.Stopped, run.Status);
    }

    [TestMethod]
    public void Cancelled_Run_Is_Terminal_And_Idempotent()
    {
        var run = CreateRun();
        run.MarkWorkStarted(T0.AddSeconds(1));

        run.Cancel(T0.AddSeconds(2));
        run.Cancel(T0.AddSeconds(3));

        Assert.AreEqual(CollectionRunStatus.Cancelled, run.Status);
        Assert.IsFalse(run.CanScheduleFollowUp);
        Assert.ThrowsExactly<InvalidOperationException>(
            () => run.Resume(T0.AddSeconds(4)));
    }

    [TestMethod]
    public void Stopped_Run_Can_Be_Cancelled_For_Cleanup()
    {
        var run = CreateRun();
        run.RequestStop(T0.AddSeconds(1));
        run.Reconcile(
            new CollectionRunTaskProgress(0, 0, 0, 0, 0, 0, 0),
            T0.AddSeconds(2));

        Assert.AreEqual(CollectionRunStatus.Stopped, run.Status);

        run.Cancel(T0.AddSeconds(3));

        Assert.AreEqual(CollectionRunStatus.Cancelled, run.Status);
        Assert.IsFalse(run.CanScheduleFollowUp);
    }

    [TestMethod]
    public async Task Cancelled_Cleanup_Returns_Repository_Delete_Count()
    {
        var repository = new StaleReconcileRepository(CreateRun());
        var service = CreateService(repository);

        var result = await service.DeleteCancelledAsync("remove cancelled test runs");

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(3, result.DeletedCount);
    }

    [TestMethod]
    public void Reconcile_Completes_Only_After_Content_Children_Are_All_Complete()
    {
        var run = CreateRun();
        run.AdvanceTo(CollectionRunStage.Content, T0.AddSeconds(1));

        run.Reconcile(
            new CollectionRunTaskProgress(
                TotalTaskCount: 3,
                PendingTaskCount: 1,
                LeasedTaskCount: 0,
                RunningTaskCount: 0,
                CompletedTaskCount: 2,
                DeadLetteredTaskCount: 0,
                CancelledTaskCount: 0),
            T0.AddSeconds(2));
        Assert.AreEqual(CollectionRunStatus.Pending, run.Status);

        run.Reconcile(
            new CollectionRunTaskProgress(3, 0, 0, 0, 3, 0, 0),
            T0.AddSeconds(3));

        Assert.AreEqual(CollectionRunStatus.Completed, run.Status);
        Assert.AreEqual(3, run.CompletedTaskCount);
        Assert.AreEqual(0, run.FailedTaskCount);
    }

    [TestMethod]
    public void Dead_Lettered_Child_Fails_Run_And_Stopping_Waits_For_In_Flight_Work()
    {
        var failed = CreateRun();
        failed.Reconcile(
            new CollectionRunTaskProgress(2, 1, 0, 0, 1, 1, 0),
            T0.AddSeconds(1));

        Assert.AreEqual(CollectionRunStatus.Failed, failed.Status);
        Assert.AreEqual(1, failed.FailedTaskCount);

        var stopping = CreateRun();
        stopping.RequestStop(T0.AddSeconds(1));
        stopping.Reconcile(
            new CollectionRunTaskProgress(2, 1, 0, 1, 0, 0, 0),
            T0.AddSeconds(2));
        Assert.AreEqual(CollectionRunStatus.Stopping, stopping.Status);

        stopping.Reconcile(
            new CollectionRunTaskProgress(2, 1, 0, 0, 1, 0, 1),
            T0.AddSeconds(3));
        Assert.AreEqual(CollectionRunStatus.Stopped, stopping.Status);
    }

    [TestMethod]
    public async Task Reconcile_Does_Not_Overwrite_Control_State_Changed_After_Read()
    {
        var run = CreateRun();
        run.MarkWorkStarted(T0.AddSeconds(1));
        var repository = new StaleReconcileRepository(run);
        var service = new CollectionRunService(
            urlResolver: null!,
            runs: repository,
            clock: new FixedClock(T0.AddSeconds(2)));

        await service.ReconcileAsync(run.Id);

        var persisted = await repository.GetAsync(run.Id);
        Assert.IsNotNull(persisted);
        Assert.AreEqual(
            CollectionRunStatus.Paused,
            persisted!.Status,
            "a stale reconciliation must not restore a state changed by a control command");
    }

    [TestMethod]
    public async Task Run_Mutation_Does_Not_Overwrite_Control_State_Changed_After_Read()
    {
        var run = CreateRun();
        run.MarkWorkStarted(T0.AddSeconds(1));
        var repository = new StaleReconcileRepository(run);
        var service = new CollectionRunService(
            urlResolver: null!,
            runs: repository,
            clock: new FixedClock(T0.AddSeconds(2)));

        await service.SetCanonicalBookAsync(run.Id, Guid.NewGuid());

        var persisted = await repository.GetAsync(run.Id);
        Assert.IsNotNull(persisted);
        Assert.AreEqual(
            CollectionRunStatus.Paused,
            persisted!.Status,
            "a stale run mutation must not restore a state changed by a control command");
    }

    [TestMethod]
    public async Task Stage_And_Work_Mutations_Preserve_Concurrent_Control_State()
    {
        var stageRun = CreateRun();
        stageRun.MarkWorkStarted(T0.AddSeconds(1));
        var stageRepository = new StaleReconcileRepository(stageRun);
        var stageService = CreateService(stageRepository);

        await stageService.AdvanceStageAsync(stageRun.Id, CollectionRunStage.Toc);

        var stagePersisted = await stageRepository.GetAsync(stageRun.Id);
        Assert.IsNotNull(stagePersisted);
        Assert.AreEqual(CollectionRunStatus.Paused, stagePersisted!.Status);

        var workRun = CreateRun();
        var workRepository = new StaleReconcileRepository(workRun);
        var workService = CreateService(workRepository);

        await workService.MarkWorkStartedAsync(workRun.Id);

        var workPersisted = await workRepository.GetAsync(workRun.Id);
        Assert.IsNotNull(workPersisted);
        Assert.AreEqual(CollectionRunStatus.Paused, workPersisted!.Status);
    }

    [TestMethod]
    public void Cancelled_Task_Is_Terminal_And_Clears_Lease()
    {
        var task = CrawlerTask.Create(
            new CrawlPayload(
                "source",
                InkFlow.Modules.Sources.Domain.SourceCapability.Content,
                new Dictionary<string, string> { ["chapterId"] = "c1" }),
            createdAt: T0);
        task.Lease("worker", T0, TimeSpan.FromMinutes(1));
        task.MarkRunning(T0.AddSeconds(1));
        task.Cancel(T0.AddSeconds(2));

        Assert.AreEqual(CrawlerTaskStatus.Cancelled, task.Status);
        Assert.IsNull(task.LeaseOwner);
        Assert.IsNull(task.LeaseExpiresAt);
        Assert.ThrowsExactly<InvalidOperationException>(() => task.Lease("worker-2", T0.AddMinutes(2), TimeSpan.FromMinutes(1)));
    }

    private static CollectionRun CreateRun() =>
        CollectionRun.Create("source", "book/42", "https://example.com/book/42", T0);

    private static CollectionRunService CreateService(StaleReconcileRepository repository) =>
        new(
            urlResolver: null!,
            runs: repository,
            clock: new FixedClock(T0.AddSeconds(2)));

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class StaleReconcileRepository(CollectionRun seed) : ICollectionRunRepository
    {
        private CollectionRunStatus persistedStatus = seed.Status;

        public Task AddAsync(CollectionRun run, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> TryAddAsync(CollectionRun run, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> TryAddWithInitialTaskAsync(
            CollectionRun run,
            CrawlerTask task,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<CollectionRun?> GetAsync(Guid id, CancellationToken cancellationToken = default)
        {
            if (id != seed.Id)
            {
                return Task.FromResult<CollectionRun?>(null);
            }

            return Task.FromResult<CollectionRun?>(CollectionRun.Rehydrate(
                seed.Id,
                seed.SourceId,
                seed.ExternalBookId,
                seed.InputUrl,
                seed.CanonicalBookId,
                persistedStatus,
                seed.Stage,
                seed.TotalTaskCount,
                seed.CompletedTaskCount,
                seed.FailedTaskCount,
                seed.LastError,
                seed.CreatedAt,
                seed.UpdatedAt));
        }

        public Task<CollectionRun?> FindActiveAsync(
            string sourceId,
            string externalBookId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public async Task<CollectionRun?> ReconcileAsync(
            Guid id,
            DateTimeOffset now,
            CancellationToken cancellationToken = default)
        {
            // The atomic repository contract observes the control commit before
            // folding progress, so it must preserve Paused rather than replaying
            // the stale Running snapshot used by SaveAsync below.
            persistedStatus = CollectionRunStatus.Paused;
            var current = await GetAsync(id, cancellationToken);
            current?.Reconcile(
                new CollectionRunTaskProgress(0, 0, 0, 0, 0, 0, 0),
                now);
            return current;
        }

        public async Task<CollectionRun?> MutateAsync(
            Guid id,
            Action<CollectionRun> mutation,
            DateTimeOffset now,
            CancellationToken cancellationToken = default)
        {
            persistedStatus = CollectionRunStatus.Paused;
            var current = await GetAsync(id, cancellationToken);
            if (current is not null)
            {
                mutation(current);
            }

            return current;
        }

        public Task<IReadOnlyList<CollectionRun>> ListAsync(
            int limit,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task SaveAsync(CollectionRun run, CancellationToken cancellationToken = default)
        {
            // Simulate a control transaction committing after the service's stale
            // read but before its non-atomic save. The old implementation then
            // writes the stale Running snapshot over the durable Paused state.
            persistedStatus = CollectionRunStatus.Paused;
            persistedStatus = run.Status;
            return Task.CompletedTask;
        }

        public Task<CollectionRunTaskProgress> GetTaskProgressAsync(
            Guid runId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new CollectionRunTaskProgress(0, 0, 0, 0, 0, 0, 0));

        public Task<int> DeleteCancelledAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(3);
    }
}
