using InkFlow.Modules.Operations.Domain;

namespace InkFlow.Modules.Operations.Application;

public static class OperationsAlertStatuses
{
    public const string Active = "active";
    public const string Resolved = "resolved";
}

public static class OperationsAlertTransitions
{
    public const string Opened = "opened";
    public const string Resolved = "resolved";
}

public sealed record OperationsAlertHistoryCursor(
    DateTimeOffset OccurredAt,
    Guid Id);

public sealed record OperationsAlertHistoryEntry(
    Guid Id,
    string Fingerprint,
    string Code,
    string Severity,
    string ResourceType,
    string ResourceId,
    string Transition,
    DateTimeOffset OccurredAt,
    int OccurrenceCount);

public sealed record OperationsAlertHistoryPage(
    IReadOnlyList<OperationsAlertHistoryEntry> Entries,
    OperationsAlertHistoryCursor? NextCursor);

/// <summary>
/// 告警历史只记录 opened/resolved 转折；重复快照更新当前 incident 的
/// last-seen/occurrence，不重复制造历史行。
/// </summary>
public interface IOperationsAlertHistoryRepository
{
    Task RecordSnapshotAsync(
        DateTimeOffset observedAt,
        bool isCompleteSnapshot,
        IReadOnlyCollection<OperationsAlertObservation> activeAlerts,
        TimeSpan retention,
        CancellationToken cancellationToken = default);

    Task<OperationsAlertHistoryPage> QueryAsync(
        int limit,
        OperationsAlertHistoryCursor? before = null,
        CancellationToken cancellationToken = default);
}
