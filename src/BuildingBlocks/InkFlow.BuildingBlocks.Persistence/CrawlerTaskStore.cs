using Microsoft.EntityFrameworkCore;

namespace InkFlow.BuildingBlocks.Persistence;

public sealed class CrawlerTaskStore(CrawlingDbContext dbContext)
{
    public async Task EnqueueAsync(CrawlerTaskRecord task, CancellationToken cancellationToken = default)
    {
        Prepare(task);
        dbContext.CrawlerTasks.Add(task);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> TryEnqueueAsync(CrawlerTaskRecord task, CancellationToken cancellationToken = default)
    {
        Prepare(task);
        var affected = await dbContext.Database.ExecuteSqlInterpolatedAsync($$"""
            INSERT INTO crawler.tasks
                (id, type, source_id, payload, idempotency_key, priority, status, attempt, max_attempts,
                 scheduled_at_utc, lease_until_utc, lease_owner, created_at_utc, updated_at_utc, last_error, trace_id)
            VALUES
                ({{task.Id}}, {{task.Type}}, {{task.SourceId}}, {{task.Payload}}, {{task.IdempotencyKey}}, {{task.Priority}},
                 {{task.Status}}, {{task.Attempt}}, {{task.MaxAttempts}}, {{task.ScheduledAtUtc}}, {{task.LeaseUntilUtc}},
                 {{task.LeaseOwner}}, {{task.CreatedAtUtc}}, {{task.UpdatedAtUtc}}, {{task.LastError}}, {{task.TraceId}})
            ON CONFLICT (idempotency_key) DO NOTHING
            """, cancellationToken);
        return affected == 1;
    }

    public async Task<CrawlerTaskRecord?> LeaseNextAsync(
        string workerId,
        DateTimeOffset now,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workerId);
        if (leaseDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(leaseDuration));
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var task = await dbContext.CrawlerTasks
            .FromSqlInterpolated($$"""
                SELECT *
                FROM crawler.tasks
                WHERE scheduled_at_utc <= {{now}}
                  AND (
                    status = {{CrawlerTaskStatuses.Pending}}
                    OR (status = {{CrawlerTaskStatuses.Leased}} AND lease_until_utc <= {{now}})
                  )
                ORDER BY priority DESC, scheduled_at_utc ASC, created_at_utc ASC
                FOR UPDATE SKIP LOCKED
                LIMIT 1
                """)
            .AsTracking()
            .SingleOrDefaultAsync(cancellationToken);

        if (task is null)
        {
            await transaction.CommitAsync(cancellationToken);
            return null;
        }

        task.Status = CrawlerTaskStatuses.Leased;
        task.Attempt++;
        task.LeaseOwner = workerId;
        task.LeaseUntilUtc = now.Add(leaseDuration);
        task.UpdatedAtUtc = now;

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return task;
    }

    public async Task MarkCompletedAsync(
        Guid taskId,
        string workerId,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        var task = await GetOwnedLeaseAsync(taskId, workerId, cancellationToken);
        task.Status = CrawlerTaskStatuses.Completed;
        task.LeaseOwner = null;
        task.LeaseUntilUtc = null;
        task.LastError = null;
        task.UpdatedAtUtc = now;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkFailedAsync(
        Guid taskId,
        string workerId,
        DateTimeOffset now,
        string error,
        TimeSpan baseBackoff,
        CancellationToken cancellationToken = default)
    {
        var task = await GetOwnedLeaseAsync(taskId, workerId, cancellationToken);
        task.LastError = string.IsNullOrWhiteSpace(error) ? "Unknown crawler failure." : error[..Math.Min(error.Length, 4000)];
        task.LeaseOwner = null;
        task.LeaseUntilUtc = null;
        task.UpdatedAtUtc = now;

        if (task.Attempt >= task.MaxAttempts)
        {
            task.Status = CrawlerTaskStatuses.DeadLetter;
        }
        else
        {
            task.Status = CrawlerTaskStatuses.Pending;
            var exponent = Math.Clamp(task.Attempt - 1, 0, 10);
            var multiplier = Math.Pow(2, exponent);
            task.ScheduledAtUtc = now.Add(TimeSpan.FromMilliseconds(baseBackoff.TotalMilliseconds * multiplier));
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<CrawlerTaskRecord?> FindAsync(Guid taskId, CancellationToken cancellationToken = default) =>
        dbContext.CrawlerTasks.AsNoTracking().SingleOrDefaultAsync(task => task.Id == taskId, cancellationToken);

    private static void Prepare(CrawlerTaskRecord task)
    {
        ArgumentNullException.ThrowIfNull(task);
        if (task.Id == Guid.Empty)
        {
            task.Id = Guid.CreateVersion7();
        }

        var now = DateTimeOffset.UtcNow;
        if (task.CreatedAtUtc == default)
        {
            task.CreatedAtUtc = now;
        }
        if (task.UpdatedAtUtc == default)
        {
            task.UpdatedAtUtc = now;
        }
        if (task.ScheduledAtUtc == default)
        {
            task.ScheduledAtUtc = now;
        }
    }

    private async Task<CrawlerTaskRecord> GetOwnedLeaseAsync(
        Guid taskId,
        string workerId,
        CancellationToken cancellationToken)
    {
        var task = await dbContext.CrawlerTasks.SingleOrDefaultAsync(task => task.Id == taskId, cancellationToken)
            ?? throw new InvalidOperationException($"Crawler task {taskId} was not found.");

        if (!string.Equals(task.Status, CrawlerTaskStatuses.Leased, StringComparison.Ordinal)
            || !string.Equals(task.LeaseOwner, workerId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Crawler task {taskId} is not leased by worker {workerId}.");
        }

        return task;
    }
}
