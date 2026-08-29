using InkFlow.Api;
using InkFlow.Modules.Operations.Application;
using InkFlow.Modules.Operations.Domain;

namespace InkFlow.UnitTests;

[TestClass]
public sealed class OperationsAlertHistoryTests
{
    private static readonly Guid CursorId =
        Guid.Parse("0198f1b3-a0c0-7b21-8a2e-0123456789ab");

    private static readonly DateTimeOffset OccurredAt =
        new(2026, 8, 29, 14, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public void History_Query_Uses_Bounded_Default_And_Opaque_Cursor()
    {
        var cursor = OperationsAlertHistoryEndpointResults.EncodeCursor(
            new OperationsAlertHistoryCursor(OccurredAt, CursorId));

        var accepted = OperationsAlertHistoryEndpointResults.TryCreateQuery(
            null,
            cursor,
            out var limit,
            out var before,
            out var error);

        Assert.IsTrue(accepted, error);
        Assert.AreEqual(OperationsAlertHistoryEndpointResults.DefaultLimit, limit);
        Assert.AreEqual(CursorId, before!.Id);
        Assert.AreEqual(OccurredAt, before.OccurredAt);
        Assert.IsFalse(cursor!.Contains("|", StringComparison.Ordinal));
    }

    [TestMethod]
    public void History_Query_Rejects_Unbounded_And_Malformed_Inputs()
    {
        foreach (var limit in new[] { 0, 101 })
        {
            var accepted = OperationsAlertHistoryEndpointResults.TryCreateQuery(
                limit,
                null,
                out _,
                out _,
                out var error);

            Assert.IsFalse(accepted);
            Assert.AreEqual("invalid_operations_alert_history_query", error);
        }

        var malformed = OperationsAlertHistoryEndpointResults.TryCreateQuery(
            null,
            "not-a-valid-cursor",
            out _,
            out _,
            out var malformedError);

        Assert.IsFalse(malformed);
        Assert.AreEqual("invalid_operations_alert_history_query", malformedError);
    }

    [TestMethod]
    public void Observation_Fingerprint_Is_Stable_And_Rejects_Unsafe_Identity()
    {
        var first = OperationsAlertObservation.Create(
            " crawler_dead_letters_present ",
            "critical",
            "crawler",
            "dead-letters");
        var second = OperationsAlertObservation.Create(
            "crawler_dead_letters_present",
            "warning",
            "crawler",
            "dead-letters");

        Assert.AreEqual(first.Fingerprint, second.Fingerprint);
        Assert.AreEqual("crawler_dead_letters_present", first.Code);
        Assert.ThrowsExactly<ArgumentException>(() => OperationsAlertObservation.Create(
            "bad\ncode",
            "critical",
            "crawler",
            "dead-letters"));
        Assert.ThrowsExactly<ArgumentException>(() => OperationsAlertObservation.Create(
            "code",
            "critical",
            "crawler",
            new string('x', OperationsAlertObservation.MaxResourceIdLength + 1)));
    }
}
