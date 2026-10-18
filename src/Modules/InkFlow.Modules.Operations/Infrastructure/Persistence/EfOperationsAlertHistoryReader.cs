using InkFlow.Modules.Operations.Application;
using Microsoft.EntityFrameworkCore;

namespace InkFlow.Modules.Operations.Infrastructure.Persistence;

/// <summary>读取告警历史分页并生成不透明游标所需的稳定投影。</summary>
internal sealed class EfOperationsAlertHistoryReader(OperationsDbContext db)
    : IOperationsAlertHistoryReader
{
    public async Task<OperationsAlertHistoryPage> QueryAsync(
        int limit,
        OperationsAlertHistoryCursor? before = null,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(limit));
        }

        var query = db.AlertHistory.AsNoTracking();
        if (before is { } cursor)
        {
            if (cursor.Id == Guid.Empty)
            {
                throw new ArgumentException("alert history cursor id must not be empty.", nameof(before));
            }

            var occurredAt = cursor.OccurredAt.ToUniversalTime();
            query = query.Where(history =>
                history.OccurredAt < occurredAt ||
                (history.OccurredAt == occurredAt &&
                 history.Id.CompareTo(cursor.Id) < 0));
        }

        var rows = await query
            .OrderByDescending(history => history.OccurredAt)
            .ThenByDescending(history => history.Id)
            .Take(limit + 1)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var hasMore = rows.Count > limit;
        var entries = rows
            .Take(limit)
            .Select(ToEntry)
            .ToList();
        var nextCursor = hasMore && entries.Count > 0
            ? new OperationsAlertHistoryCursor(entries[^1].OccurredAt, entries[^1].Id)
            : null;

        return new OperationsAlertHistoryPage(entries, nextCursor);
    }

    private static OperationsAlertHistoryEntry ToEntry(OperationsAlertHistoryEntity entity) =>
        new(
            entity.Id,
            entity.Fingerprint,
            entity.Code,
            entity.Severity,
            entity.ResourceType,
            entity.ResourceId,
            entity.Transition,
            entity.OccurredAt,
            entity.OccurrenceCount);
}
