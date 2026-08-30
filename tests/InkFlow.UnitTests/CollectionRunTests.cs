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
}
