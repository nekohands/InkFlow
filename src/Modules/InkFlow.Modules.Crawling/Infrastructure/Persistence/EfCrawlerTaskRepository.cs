using InkFlow.Modules.Crawling.Application;
using InkFlow.Modules.Crawling.Domain;
using InkFlow.Modules.Crawling.Infrastructure.Persistence;
using InkFlow.Modules.Sources.Domain;
using Microsoft.EntityFrameworkCore;

namespace InkFlow.Modules.Crawling.Infrastructure.Persistence;

/// <summary>EF Core / Npgsql 仓储实现。租约互斥依赖数据库事务 + 状态条件更新。</summary>
public sealed class EfCrawlerTaskRepository(CrawlingDbContext db) : ICrawlerTaskRepository
{
    public async Task AddAsync(CrawlerTask task, CancellationToken cancellationToken = default)
    {
        db.Tasks.Add(CrawlerTaskMapper.ToEntity(task));
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<CrawlerTask?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await db.Tasks.FindAsync([id], cancellationToken).ConfigureAwait(false);
        return entity is null ? null : CrawlerTaskMapper.ToDomain(entity);
    }

    public async Task SaveAsync(CrawlerTask task, CancellationToken cancellationToken = default)
    {
        var entity = await db.Tasks.FindAsync([task.Id], cancellationToken).ConfigureAwait(false)
                     ?? throw new InvalidOperationException(
                         $"crawler task {task.Id} does not exist; use {nameof(AddAsync)} first.");

        CrawlerTaskMapper.ApplyDomain(task, entity);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<CrawlerTask>> FindLeasableAsync(DateTimeOffset now, int limit, CancellationToken cancellationToken = default)
    {
        var entities = await db.Tasks
            .Where(t =>
                t.Status == (int)CrawlerTaskStatus.Pending ||
                ((t.Status == (int)CrawlerTaskStatus.Leased ||
                  t.Status == (int)CrawlerTaskStatus.Running) &&
                 t.LeaseExpiresAt != null && t.LeaseExpiresAt <= now))
            .OrderBy(t => t.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(CrawlerTaskMapper.ToDomain).ToList();
    }

    public async Task AddDeadLetterAsync(DeadLetterTask deadLetter, CancellationToken cancellationToken = default)
    {
        db.DeadLetters.Add(new DeadLetterEntity
        {
            Id = deadLetter.Id,
            TaskId = deadLetter.TaskId,
            SourceId = deadLetter.SourceId,
            Reason = deadLetter.Reason,
            AttemptCount = deadLetter.AttemptCount,
            DeadLetteredAt = deadLetter.DeadLetteredAt,
        });

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> HasActiveTaskAsync(
        string sourceId, SourceCapability capability, CancellationToken cancellationToken = default)
    {
        var activeStatuses = new[]
        {
            (int)CrawlerTaskStatus.Pending,
            (int)CrawlerTaskStatus.Leased,
            (int)CrawlerTaskStatus.Running,
        };

        return await db.Tasks
            .AnyAsync(t =>
                t.SourceId == sourceId &&
                t.Capability == (int)capability &&
                activeStatuses.Contains(t.Status),
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<DeadLetterTask>> ListDeadLettersAsync(int limit, CancellationToken cancellationToken = default)
    {
        var entities = await db.DeadLetters
            .OrderByDescending(d => d.DeadLetteredAt)
            .Take(limit)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities
            .Select(e => new DeadLetterTask(e.Id, e.TaskId, e.SourceId, e.Reason, e.AttemptCount, e.DeadLetteredAt))
            .ToList();
    }
}
