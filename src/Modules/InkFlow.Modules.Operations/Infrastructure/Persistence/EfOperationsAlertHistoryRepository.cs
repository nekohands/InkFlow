using InkFlow.Modules.Operations.Application;
using InkFlow.Modules.Operations.Domain;

namespace InkFlow.Modules.Operations.Infrastructure.Persistence;

/// <summary>
/// 兼容现有复合仓储入口；快照写入和历史读取分别由同一 scoped DbContext
/// 上的专用职责实现承载。
/// </summary>
public sealed class EfOperationsAlertHistoryRepository(OperationsDbContext db)
    : IOperationsAlertHistoryRepository
{
    private readonly EfOperationsAlertHistoryRecorder recorder = new(db);
    private readonly EfOperationsAlertHistoryReader reader = new(db);

    public Task RecordSnapshotAsync(
        DateTimeOffset observedAt,
        bool isCompleteSnapshot,
        IReadOnlyCollection<OperationsAlertObservation> activeAlerts,
        TimeSpan retention,
        CancellationToken cancellationToken = default) =>
        recorder.RecordSnapshotAsync(
            observedAt,
            isCompleteSnapshot,
            activeAlerts,
            retention,
            cancellationToken);

    public Task<OperationsAlertHistoryPage> QueryAsync(
        int limit,
        OperationsAlertHistoryCursor? before = null,
        CancellationToken cancellationToken = default) =>
        reader.QueryAsync(limit, before, cancellationToken);
}
