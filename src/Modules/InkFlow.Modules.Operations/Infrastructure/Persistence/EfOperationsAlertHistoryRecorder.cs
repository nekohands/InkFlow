using InkFlow.Modules.Operations.Application;
using InkFlow.Modules.Operations.Domain;
using Microsoft.EntityFrameworkCore;

namespace InkFlow.Modules.Operations.Infrastructure.Persistence;

/// <summary>
/// 在一个 PostgreSQL 事务中记录告警快照、incident 转折和历史保留清理。
/// </summary>
internal sealed class EfOperationsAlertHistoryRecorder(OperationsDbContext db)
    : IOperationsAlertHistoryRecorder
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
}
