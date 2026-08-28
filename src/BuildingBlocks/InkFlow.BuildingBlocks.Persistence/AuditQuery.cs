using InkFlow.BuildingBlocks.Security;
using Microsoft.EntityFrameworkCore;

namespace InkFlow.BuildingBlocks.Persistence;

/// <summary>审计查询的稳定排序游标。OccurredAt 与 Id 共同构成唯一顺序。</summary>
public sealed record AuditEventCursor(DateTimeOffset OccurredAt, Guid Id);

/// <summary>
/// 有界审计查询。读端只允许精确过滤，不提供任意 SQL、全文或无上限分页。
/// </summary>
public sealed record AuditEventQuery(
    DateTimeOffset? From,
    DateTimeOffset? To,
    string? Action,
    string? Outcome,
    string? ActorId,
    AuditEventCursor? Before,
    int Limit)
{
    public const int DefaultLimit = 50;
    public const int MaxLimit = 100;

    public void Validate()
    {
        if (Limit is < 1 or > MaxLimit)
        {
            throw new ArgumentOutOfRangeException(nameof(Limit));
        }

        if (From is not null && To is not null && From > To)
        {
            throw new ArgumentException("audit query range is reversed.", nameof(From));
        }

        if (Before is { Id: Guid id } && id == Guid.Empty)
        {
            throw new ArgumentException("audit query cursor id must not be empty.", nameof(Before));
        }

        ValidateFilter(Action, 128, nameof(Action));
        ValidateFilter(Outcome, 64, nameof(Outcome));
        ValidateFilter(ActorId, 256, nameof(ActorId));
    }

    private static void ValidateFilter(string? value, int maxLength, string name)
    {
        if (value is not null && (string.IsNullOrWhiteSpace(value) || value.Length > maxLength))
        {
            throw new ArgumentException("audit query filter is empty or too long.", name);
        }
    }
}

public sealed record AuditEventPage(
    IReadOnlyList<AuditEvent> Events,
    AuditEventCursor? NextCursor);

/// <summary>审计事实只读端口；写入仍只能通过追加式 sink。</summary>
public interface IAuditEventReader
{
    Task<AuditEventPage> QueryAsync(
        AuditEventQuery query,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// PostgreSQL 审计读端。查询始终使用无跟踪、有限 Take 和稳定游标，避免把审计表当作可变 CRUD 资源。
/// </summary>
public sealed class EfAuditEventReader(AuditDbContext db) : IAuditEventReader
{
    public async Task<AuditEventPage> QueryAsync(
        AuditEventQuery query,
        CancellationToken cancellationToken = default)
    {
        query.Validate();

        var events = db.Events.AsNoTracking().AsQueryable();
        if (query.From is { } from)
        {
            events = events.Where(eventEntity => eventEntity.OccurredAt >= from);
        }

        if (query.To is { } to)
        {
            events = events.Where(eventEntity => eventEntity.OccurredAt <= to);
        }

        if (!string.IsNullOrWhiteSpace(query.Action))
        {
            events = events.Where(eventEntity => eventEntity.Action == query.Action);
        }

        if (!string.IsNullOrWhiteSpace(query.Outcome))
        {
            events = events.Where(eventEntity => eventEntity.Outcome == query.Outcome);
        }

        if (!string.IsNullOrWhiteSpace(query.ActorId))
        {
            events = events.Where(eventEntity => eventEntity.ActorId == query.ActorId);
        }

        if (query.Before is { } before)
        {
            events = events.Where(eventEntity =>
                eventEntity.OccurredAt < before.OccurredAt ||
                (eventEntity.OccurredAt == before.OccurredAt &&
                 eventEntity.Id.CompareTo(before.Id) < 0));
        }

        var rows = await events
            .OrderByDescending(eventEntity => eventEntity.OccurredAt)
            .ThenByDescending(eventEntity => eventEntity.Id)
            .Take(query.Limit + 1)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var hasMore = rows.Count > query.Limit;
        var pageRows = rows
            .Take(query.Limit)
            .Select(ToDomain)
            .ToList();
        var nextCursor = hasMore && pageRows.Count > 0
            ? new AuditEventCursor(pageRows[^1].OccurredAt, pageRows[^1].Id)
            : null;

        return new AuditEventPage(pageRows, nextCursor);
    }

    private static AuditEvent ToDomain(AuditEventEntity entity) => new()
    {
        Id = entity.Id,
        OccurredAt = entity.OccurredAt,
        ActorType = entity.ActorType,
        ActorId = entity.ActorId,
        Action = entity.Action,
        Resource = entity.Resource,
        Outcome = entity.Outcome,
        StatusCode = entity.StatusCode,
        Reason = entity.Reason,
        TraceId = entity.TraceId,
        Reference = entity.Reference,
    };
}
