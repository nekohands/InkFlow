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

/// <summary>告警快照写入端口：负责 incident 状态转移、历史追加与保留清理。</summary>
public interface IOperationsAlertHistoryRecorder
{
    Task RecordSnapshotAsync(
        DateTimeOffset observedAt,
        bool isCompleteSnapshot,
        IReadOnlyCollection<OperationsAlertObservation> activeAlerts,
        TimeSpan retention,
        CancellationToken cancellationToken = default);
}

/// <summary>告警历史读取端口：只负责有界、稳定排序的历史分页。</summary>
public interface IOperationsAlertHistoryReader
{
    Task<OperationsAlertHistoryPage> QueryAsync(
        int limit,
        OperationsAlertHistoryCursor? before = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 兼容现有直接注入/构造方的复合端口；新调用方按读写职责依赖窄端口。
/// </summary>
public interface IOperationsAlertHistoryRepository :
    IOperationsAlertHistoryRecorder,
    IOperationsAlertHistoryReader
{
}
