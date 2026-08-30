using InkFlow.Modules.Content.Domain;

namespace InkFlow.UnitTests;

[TestClass]
public sealed class BookPackageJobTests
{
    private static readonly DateTimeOffset T0 =
        new(2026, 8, 31, 10, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public void Expired_Running_Lease_Counts_As_A_New_Attempt()
    {
        var job = BookPackageJob.Create(
            Guid.CreateVersion7(),
            BookPackageFormat.Epub,
            T0,
            T0.AddDays(1),
            maxAttempts: 2);

        job.Lease("worker-a", T0, TimeSpan.FromMinutes(1));
        job.Lease("worker-b", T0.AddMinutes(2), TimeSpan.FromMinutes(1));

        Assert.AreEqual(BookPackageJobStatus.Running, job.Status);
        Assert.AreEqual(2, job.AttemptCount);
        Assert.AreEqual("worker-b", job.LeaseOwner);
    }

    [TestMethod]
    public void Expired_Running_Lease_At_Max_Attempts_Becomes_Failed()
    {
        var job = BookPackageJob.Create(
            Guid.CreateVersion7(),
            BookPackageFormat.Txt,
            T0,
            T0.AddDays(1),
            maxAttempts: 1);

        job.Lease("worker-a", T0, TimeSpan.FromMinutes(1));
        job.FailExpiredLease(T0.AddMinutes(2));

        Assert.AreEqual(BookPackageJobStatus.Failed, job.Status);
        Assert.AreEqual("package lease expired after maximum attempts.", job.FailureReason);
        Assert.IsNull(job.LeaseOwner);
        Assert.IsNull(job.LeaseExpiresAt);
    }
}
