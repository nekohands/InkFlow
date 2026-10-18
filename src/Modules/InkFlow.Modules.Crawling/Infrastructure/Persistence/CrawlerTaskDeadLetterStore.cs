using System.Data;
using InkFlow.Modules.Crawling.Application;
using InkFlow.Modules.Crawling.Domain;
using Microsoft.EntityFrameworkCore;

namespace InkFlow.Modules.Crawling.Infrastructure.Persistence;

/// <summary>
/// Owns dead-letter persistence and replay. Task leasing and dedupe stay in
/// <see cref="EfCrawlerTaskRepository"/> because they share different locks.
/// </summary>
internal sealed class CrawlerTaskDeadLetterStore(CrawlingDbContext db)
{
    public async Task AddAsync(
        DeadLetterTask deadLetter,
        CancellationToken cancellationToken = default)
    {
        AddDeadLetterRow(deadLetter);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task AddWithTaskAsync(
        DeadLetterTask deadLetter,
        CrawlerTask task,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(task);

        await using var transaction = await db.Database
            .BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken)
            .ConfigureAwait(false);

        AddDeadLetterRow(deadLetter);

        var entity = await db.Tasks.FindAsync([task.Id], cancellationToken).ConfigureAwait(false)
                     ?? throw new InvalidOperationException(
                         $"crawler task {task.Id} does not exist; use {nameof(AddAsync)} first.");
        CrawlerTaskMapper.ApplyDomain(task, entity);

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<DeadLetterTask>> ListAsync(
        int limit,
        CancellationToken cancellationToken = default)
    {
        var entities = await db.DeadLetters
            .OrderByDescending(d => d.DeadLetteredAt)
            .Take(limit)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities
            .Select(e => new DeadLetterTask(
                e.Id,
                e.TaskId,
                e.SourceId,
                e.Reason,
                e.AttemptCount,
                e.DeadLetteredAt,
                e.ReplayTaskId,
                e.ReplayedAt,
                e.ReplayRequestedBy,
                e.ReplayReason))
            .ToList();
    }

    private void AddDeadLetterRow(DeadLetterTask deadLetter)
    {
        db.DeadLetters.Add(new DeadLetterEntity
        {
            Id = deadLetter.Id,
            TaskId = deadLetter.TaskId,
            SourceId = deadLetter.SourceId,
            Reason = deadLetter.Reason,
            AttemptCount = deadLetter.AttemptCount,
            DeadLetteredAt = deadLetter.DeadLetteredAt,
            ReplayTaskId = deadLetter.ReplayTaskId,
            ReplayedAt = deadLetter.ReplayedAt,
            ReplayRequestedBy = deadLetter.ReplayRequestedBy,
            ReplayReason = deadLetter.ReplayReason,
        });
    }
}
