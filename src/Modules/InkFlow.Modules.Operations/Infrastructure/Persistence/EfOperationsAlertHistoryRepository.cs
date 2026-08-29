using InkFlow.Modules.Operations.Application;
using InkFlow.Modules.Operations.Domain;
using Microsoft.EntityFrameworkCore;

namespace InkFlow.Modules.Operations.Infrastructure.Persistence;

/// <summary>
/// 告警状态和历史在 PostgreSQL 中协调。快照锁保证多 API 实例不会同时把同一
/// incident 打开/恢复两次；历史表只允许追加，过期行由保留策略删除。
/// </summary>
public sealed class EfOperationsAlertHistoryRepository(OperationsDbContext db)
    : IOperationsAlertHistoryRepository
{
    private const int AdvisoryLockNamespace = 9102;
    private const int AdvisoryLockKey = 1;

    public async Task RecordSnapshotAsync(
        DateTimeOffset observedAt,
        bool isCompleteSnapshot,
        IReadOnlyCollection<OperationsAlertObservation> activeAlerts,
        TimeSpan retention,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(activeAlerts);
        if (retention <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(retention));
        }

        observedAt = observedAt.ToUniversalTime();
        var observations = activeAlerts
            .GroupBy(alert => alert.Fingerprint, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToDictionary(alert => alert.Fingerprint, StringComparer.Ordinal);

        await using var transaction = await db.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);
        await db.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock({AdvisoryLockNamespace}, {AdvisoryLockKey})",
                cancellationToken)
            .ConfigureAwait(false);

        var incidents = await db.AlertIncidents
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var incidentByFingerprint = incidents.ToDictionary(
            incident => incident.Fingerprint,
            StringComparer.Ordinal);

        foreach (var observation in observations.Values)
        {
            if (!incidentByFingerprint.TryGetValue(observation.Fingerprint, out var incident))
            {
                incident = new OperationsAlertIncidentEntity
                {
                    Fingerprint = observation.Fingerprint,
                    Code = observation.Code,
                    Severity = observation.Severity,
                    ResourceType = observation.ResourceType,
                    ResourceId = observation.ResourceId,
                    Status = OperationsAlertStatuses.Active,
                    FirstSeenAt = observedAt,
                    LastSeenAt = observedAt,
                    LastTransitionAt = observedAt,
                    OccurrenceCount = 1,
                };
                db.AlertIncidents.Add(incident);
                db.AlertHistory.Add(CreateHistory(incident, OperationsAlertTransitions.Opened, observedAt));
                incidentByFingerprint.Add(incident.Fingerprint, incident);
                continue;
            }

            if (observedAt <= incident.LastSeenAt)
            {
                continue;
            }

            incident.Code = observation.Code;
            incident.Severity = observation.Severity;
            incident.ResourceType = observation.ResourceType;
            incident.ResourceId = observation.ResourceId;
            incident.LastSeenAt = observedAt;
            incident.OccurrenceCount = Increment(incident.OccurrenceCount);
            if (incident.Status == OperationsAlertStatuses.Resolved)
            {
                incident.Status = OperationsAlertStatuses.Active;
                incident.LastTransitionAt = observedAt;
                incident.LastResolvedAt = null;
                db.AlertHistory.Add(CreateHistory(incident, OperationsAlertTransitions.Opened, observedAt));
            }
        }

        if (isCompleteSnapshot)
        {
            foreach (var incident in incidents.Where(incident =>
                         incident.Status == OperationsAlertStatuses.Active &&
                         !observations.ContainsKey(incident.Fingerprint) &&
                         incident.LastSeenAt <= observedAt))
            {
                incident.Status = OperationsAlertStatuses.Resolved;
                incident.LastTransitionAt = observedAt;
                incident.LastResolvedAt = observedAt;
                db.AlertHistory.Add(CreateHistory(incident, OperationsAlertTransitions.Resolved, observedAt));
            }
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var cutoff = observedAt - retention;
        await db.AlertHistory
            .Where(history => history.OccurredAt < cutoff)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
        await db.AlertIncidents
            .Where(incident =>
                incident.Status == OperationsAlertStatuses.Resolved &&
                incident.LastTransitionAt < cutoff)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
        db.ChangeTracker.Clear();

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

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

    private static OperationsAlertHistoryEntity CreateHistory(
        OperationsAlertIncidentEntity incident,
        string transition,
        DateTimeOffset occurredAt) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            Fingerprint = incident.Fingerprint,
            Code = incident.Code,
            Severity = incident.Severity,
            ResourceType = incident.ResourceType,
            ResourceId = incident.ResourceId,
            Transition = transition,
            OccurredAt = occurredAt,
            OccurrenceCount = incident.OccurrenceCount,
        };

    private static int Increment(int value) => value == int.MaxValue ? int.MaxValue : value + 1;

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
