namespace InkFlow.Modules.Operations.Application;

public sealed class ConsistencyCheckService(
    IConsistencySnapshotReader snapshotReader,
    TimeProvider clock,
    ConsistencyValueDefinitions values) : IConsistencyCheckService
{
    public const int MaxReturnedIssues = 1_000;

    public async Task<ConsistencyCheckReport> CheckAsync(
        CancellationToken cancellationToken = default)
    {
        var snapshot = await snapshotReader
            .ReadAsync(cancellationToken)
            .ConfigureAwait(false);
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(values);
        var evaluation = new ConsistencyRuleEvaluation(snapshot, values, MaxReturnedIssues);
        var returnedIssues = evaluation.Run();

        return new ConsistencyCheckReport(
            clock.GetUtcNow(),
            evaluation.TotalIssueCount == 0 ? "healthy" : "issues_found",
            evaluation.TotalIssueCount,
            returnedIssues.Count,
            returnedIssues.Count < evaluation.TotalIssueCount,
            returnedIssues);
    }
}
