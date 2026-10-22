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
        var allIssues = ConsistencyCheckValidator.Validate(snapshot, values);
        var returnedIssues = allIssues.Take(MaxReturnedIssues).ToList();

        return new ConsistencyCheckReport(
            clock.GetUtcNow(),
            allIssues.Count == 0 ? "healthy" : "issues_found",
            allIssues.Count,
            returnedIssues.Count,
            returnedIssues.Count < allIssues.Count,
            returnedIssues);
    }
}
