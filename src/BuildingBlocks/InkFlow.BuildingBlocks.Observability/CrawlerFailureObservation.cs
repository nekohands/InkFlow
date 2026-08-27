namespace InkFlow.BuildingBlocks.Observability;

/// <summary>抓取失败的低基数观测维度；不携带原始异常文本，避免把敏感值带入日志或指标。</summary>
public sealed record CrawlerFailureObservation(
    Guid TaskId,
    string SourceId,
    string Capability,
    int AttemptCount,
    int MaxAttempts,
    string Disposition,
    string FailureKind,
    DateTimeOffset OccurredAt)
{
    public static CrawlerFailureObservation Create(
        Guid taskId,
        string sourceId,
        string capability,
        int attemptCount,
        int maxAttempts,
        string disposition,
        string reason,
        DateTimeOffset occurredAt) =>
        new(
            taskId,
            sourceId,
            capability,
            attemptCount,
            maxAttempts,
            disposition,
            Classify(reason),
            occurredAt);

    private static string Classify(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return "unknown";
        }

        if (reason.Contains("publish", StringComparison.OrdinalIgnoreCase))
        {
            return "publishing";
        }

        if (reason.StartsWith("ssrf:", StringComparison.OrdinalIgnoreCase))
        {
            return "ssrf";
        }

        if (reason.StartsWith("http:", StringComparison.OrdinalIgnoreCase))
        {
            return reason.Contains("upstream returned", StringComparison.OrdinalIgnoreCase)
                ? "upstream_status"
                : "transport";
        }

        if (reason.Contains("missing", StringComparison.OrdinalIgnoreCase))
        {
            return "payload";
        }

        if (reason.Contains("does not exist", StringComparison.OrdinalIgnoreCase) ||
            reason.Contains("no rule", StringComparison.OrdinalIgnoreCase) ||
            reason.Contains("no rule DSL", StringComparison.OrdinalIgnoreCase))
        {
            return "source_configuration";
        }

        return "execution";
    }
}

/// <summary>失败后的任务处置状态，作为日志和指标的稳定低基数维度。</summary>
public static class CrawlerFailureDisposition
{
    public const string Retry = "retry";
    public const string DeadLetter = "dead_letter";
    public const string NotRunning = "not_running";
}
