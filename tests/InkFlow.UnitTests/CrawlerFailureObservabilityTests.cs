using InkFlow.BuildingBlocks.Observability;
using Microsoft.Extensions.Logging.Abstractions;

namespace InkFlow.UnitTests;

[TestClass]
public sealed class CrawlerFailureObservabilityTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 28, 0, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public void Observation_Uses_Low_Cardinality_Failure_Kind_Without_Raw_Reason()
    {
        var observation = CrawlerFailureObservation.Create(
            Guid.NewGuid(),
            "linovelib",
            "Content",
            attemptCount: 2,
            maxAttempts: 3,
            CrawlerFailureDisposition.Retry,
            "content publish failed: token=secret-value",
            T0);

        Assert.AreEqual("publishing", observation.FailureKind);
        Assert.IsFalse(observation.ToString().Contains("secret-value", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Reporter_Fans_Out_To_All_Sinks()
    {
        var first = new RecordingSink();
        var second = new RecordingSink();
        var reporter = new CrawlerFailureReporter(
            [first, second],
            NullLogger<CrawlerFailureReporter>.Instance);
        var observation = Observation();

        reporter.Report(observation);

        Assert.AreEqual(1, first.Observations.Count);
        Assert.AreEqual(1, second.Observations.Count);
        Assert.AreEqual(observation, first.Observations[0]);
    }

    [TestMethod]
    public void Reporter_Isolates_A_Failing_Sink()
    {
        var recording = new RecordingSink();
        var reporter = new CrawlerFailureReporter(
            [new ThrowingSink(), recording],
            NullLogger<CrawlerFailureReporter>.Instance);

        reporter.Report(Observation());

        Assert.AreEqual(1, recording.Observations.Count);
    }

    private static CrawlerFailureObservation Observation() =>
        CrawlerFailureObservation.Create(
            Guid.NewGuid(),
            "official-a",
            "Content",
            3,
            3,
            CrawlerFailureDisposition.DeadLetter,
            "http: upstream returned status 503.",
            T0);

    private sealed class RecordingSink : ICrawlerFailureSink
    {
        public List<CrawlerFailureObservation> Observations { get; } = [];

        public void Record(CrawlerFailureObservation observation) => Observations.Add(observation);
    }

    private sealed class ThrowingSink : ICrawlerFailureSink
    {
        public void Record(CrawlerFailureObservation observation) =>
            throw new InvalidOperationException("fixture sink failure");
    }
}
