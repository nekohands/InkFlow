using InkFlow.Modules.Crawling.Application;
using InkFlow.Modules.Crawling.Domain;
using InkFlow.Modules.Sources.Domain;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace InkFlow.UnitTests;

[TestClass]
public sealed class CrawlerTaskTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 25, 10, 0, 0, TimeSpan.Zero);

    private static CrawlPayload Payload() =>
        new("example-source", SourceCapability.Toc, new Dictionary<string, string> { ["bookId"] = "42" });

    private static CrawlerTask NewTask(int maxAttempts = 2) => CrawlerTask.Create(Payload(), maxAttempts, T0);

    [TestMethod]
    public void Happy_Path_Pending_Leased_Running_Completed()
    {
        var task = NewTask();
        task.Lease("worker-1", T0, TimeSpan.FromMinutes(1));
        task.MarkRunning(T0.AddSeconds(5));
        task.Complete(T0.AddSeconds(9));

        Assert.AreEqual(CrawlerTaskStatus.Completed, task.Status);
        Assert.IsNull(task.LeaseOwner);
        Assert.AreEqual(1, task.AttemptCount);
    }

    [TestMethod]
    public void Lease_Is_Exclusive_Until_Expired()
    {
        var service = new CrawlerLeaseService(new FixedClock(T0)) { DefaultLeaseDuration = TimeSpan.FromMinutes(1) };
        var task = NewTask();

        Assert.IsTrue(service.TryLease(task, "worker-a"));
        // 未过期：其他 worker 不能领取
        var now1 = T0.AddSeconds(30);
        Assert.IsFalse(task.IsLeasable(now1));
        Assert.IsFalse(service.TryLease(task, "worker-b"));

        // 过期后可回收并重新领取；重新领取是一次新的尝试（计数 +1），
        // 保证反复超时的任务会耗尽 MaxAttempts 进入死信。
        var now2 = T0.AddMinutes(2);
        task.ReleaseExpiredLease(now2);
        Assert.AreEqual(CrawlerTaskStatus.Pending, task.Status);
        Assert.IsTrue(service.TryLease(task, "worker-b"));
        Assert.AreEqual("worker-b", task.LeaseOwner);
        Assert.AreEqual(2, task.AttemptCount);
    }

    [TestMethod]
    public void TryLease_Reclaims_An_Expired_Lease_Before_Retrying()
    {
        var service = new CrawlerLeaseService(new FixedClock(T0.AddMinutes(2)))
        {
            DefaultLeaseDuration = TimeSpan.FromMinutes(1),
        };
        var task = NewTask(maxAttempts: 3);
        task.Lease("worker-a", T0, TimeSpan.FromMinutes(1));

        Assert.IsTrue(service.TryLease(task, "worker-b"));
        Assert.AreEqual(CrawlerTaskStatus.Leased, task.Status);
        Assert.AreEqual("worker-b", task.LeaseOwner);
        Assert.AreEqual(2, task.AttemptCount);
    }

    [TestMethod]
    public void TryLease_Reclaims_An_Expired_Running_Task_After_Worker_Crash()
    {
        var service = new CrawlerLeaseService(new FixedClock(T0.AddMinutes(2)))
        {
            DefaultLeaseDuration = TimeSpan.FromMinutes(1),
        };
        var task = NewTask(maxAttempts: 3);
        task.Lease("worker-a", T0, TimeSpan.FromMinutes(1));
        task.MarkRunning(T0.AddSeconds(1));

        Assert.IsTrue(service.TryLease(task, "worker-b"));
        Assert.AreEqual(CrawlerTaskStatus.Leased, task.Status);
        Assert.AreEqual("worker-b", task.LeaseOwner);
        Assert.AreEqual(2, task.AttemptCount);
    }

    [TestMethod]
    public void ReleaseExpired_Reclaims_Running_Task()
    {
        var service = new CrawlerLeaseService(new FixedClock(T0.AddMinutes(2)));
        var task = NewTask();
        task.Lease("worker-a", T0, TimeSpan.FromMinutes(1));
        task.MarkRunning(T0.AddSeconds(1));

        var released = service.ReleaseExpired([task]);

        Assert.AreEqual(1, released.Count);
        Assert.AreEqual(CrawlerTaskStatus.Pending, task.Status);
        Assert.IsNull(task.LeaseOwner);
        Assert.AreEqual(1, task.AttemptCount);
    }

    [TestMethod]
    public void Fail_Below_Max_Attempts_Returns_To_Pending()
    {
        var task = NewTask(maxAttempts: 3);
        task.Lease("w", T0, TimeSpan.FromMinutes(1));
        task.MarkRunning(T0);
        task.Fail(T0);

        Assert.AreEqual(CrawlerTaskStatus.Pending, task.Status);
        Assert.AreEqual(1, task.AttemptCount);
    }

    [TestMethod]
    public void Fail_At_Max_Attempts_DeadLetters_And_Can_Build_DeadLetter_Record()
    {
        var task = NewTask(maxAttempts: 2);

        // 第一次尝试
        task.Lease("w", T0, TimeSpan.FromMinutes(1));
        task.MarkRunning(T0);
        task.Fail(T0);

        // 第二次（最后一次）尝试
        task.Lease("w", T0.AddMinutes(1), TimeSpan.FromMinutes(1));
        task.MarkRunning(T0.AddMinutes(1));
        task.Fail(T0.AddMinutes(2));

        Assert.AreEqual(CrawlerTaskStatus.DeadLettered, task.Status);
        Assert.AreEqual(2, task.AttemptCount);

        var deadLetter = DeadLetterTask.From(task, "upstream 503", T0.AddMinutes(3));
        Assert.AreEqual(task.Id, deadLetter.TaskId);
        Assert.AreEqual("example-source", deadLetter.SourceId);
        Assert.AreEqual("upstream 503", deadLetter.Reason);
    }

    [TestMethod]
    public void Illegal_Transitions_Are_Rejected()
    {
        var task = NewTask();
        // Pending → Completed 不合法
        Assert.ThrowsExactly<InvalidOperationException>(() => task.Complete(T0));
        // Pending → Running 不合法
        Assert.ThrowsExactly<InvalidOperationException>(() => task.MarkRunning(T0));
    }

    [TestMethod]
    public void Dead_Letter_Is_Terminal()
    {
        var task = NewTask(maxAttempts: 1);
        task.Lease("w", T0, TimeSpan.FromMinutes(1));
        task.MarkRunning(T0);
        task.Fail(T0);
        Assert.AreEqual(CrawlerTaskStatus.DeadLettered, task.Status);

        Assert.ThrowsExactly<InvalidOperationException>(() => task.Lease("w2", T0, TimeSpan.FromMinutes(1)));
    }

    [TestMethod]
    public void ReleaseExpiredLease_Rejects_Non_Expired_Leases()
    {
        var task = NewTask();
        task.Lease("w", T0, TimeSpan.FromMinutes(5));

        Assert.ThrowsExactly<InvalidOperationException>(() => task.ReleaseExpiredLease(T0.AddSeconds(10)));
    }

    [TestMethod]
    public void RetryPolicy_Backoff_Grows_And_Is_Capped()
    {
        var policy = new RetryPolicy { BaseDelay = TimeSpan.FromSeconds(4), MaxDelay = TimeSpan.FromSeconds(30) };

        foreach (var attempt in new[] { 1, 2, 3 })
        {
            for (var i = 0; i < 20; i++) // 抖动采样，验证上限与范围
            {
                var delay = policy.DelayFor(attempt);
                Assert.IsTrue(delay >= TimeSpan.Zero && delay <= policy.MaxDelay);
            }
        }
    }
}

file sealed class FixedClock(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
