using InkFlow.Modules.Crawling.Application;

namespace InkFlow.UnitTests;

[TestClass]
public sealed class DeadLetterReplayTests
{
    [TestMethod]
    public void Command_Normalizes_Operator_And_Reason_Without_Line_Breaks()
    {
        var command = DeadLetterReplayCommand.Create(
            Guid.NewGuid(),
            " operator-1\n",
            " upstream recovered\r\n");

        Assert.AreEqual("operator-1", command.RequestedBy);
        Assert.AreEqual("upstream recovered", command.ReplayReason);
    }

    [TestMethod]
    public void Command_Rejects_Empty_Identity()
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            DeadLetterReplayCommand.Create(Guid.Empty, "operator-1", "retry"));
        Assert.ThrowsExactly<ArgumentException>(() =>
            DeadLetterReplayCommand.Create(Guid.NewGuid(), " ", "retry"));
        Assert.ThrowsExactly<ArgumentException>(() =>
            DeadLetterReplayCommand.Create(Guid.NewGuid(), "operator-1", " "));
    }

    [TestMethod]
    public void Already_Replayed_Result_Is_A_Successful_Idempotent_Outcome()
    {
        var replayTaskId = Guid.NewGuid();
        var result = new DeadLetterReplayResult(
            DeadLetterReplayStatus.AlreadyReplayed,
            replayTaskId);

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(replayTaskId, result.ReplayTaskId);
    }
}
