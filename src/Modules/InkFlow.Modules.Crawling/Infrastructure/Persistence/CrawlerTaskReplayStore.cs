using InkFlow.Modules.Crawling.Application;
using InkFlow.Modules.Crawling.Domain;
using Microsoft.EntityFrameworkCore;

namespace InkFlow.Modules.Crawling.Infrastructure.Persistence;

/// <summary>Owns the transactional operator repair flow for a dead letter.</summary>
internal sealed class CrawlerTaskReplayStore(CrawlingDbContext db)
{
    public async Task<DeadLetterReplayResult> ReplayAsync(
        DeadLetterReplayCommand command,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);

        // 重放是修复命令：锁住死信行，避免两个操作员同时创建两个重放任务。
        var deadLetter = await db.DeadLetters
            .FromSqlInterpolated($"""
                SELECT *
                FROM "crawler"."dead_letters"
                WHERE "Id" = {command.DeadLetterId}
                FOR UPDATE
                """)
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (deadLetter is null)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new(DeadLetterReplayStatus.NotFound);
        }

        if (deadLetter.ReplayTaskId is { } existingReplayTaskId)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new(DeadLetterReplayStatus.AlreadyReplayed, existingReplayTaskId);
        }

        // 锁住原任务并再次核对状态，防止修复入口绕过聚合状态机。
        var taskEntity = await db.Tasks
            .FromSqlInterpolated($"""
                SELECT *
                FROM "crawler"."tasks"
                WHERE "Id" = {deadLetter.TaskId}
                FOR UPDATE
                """)
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (taskEntity is null)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new(DeadLetterReplayStatus.OriginalTaskMissing);
        }

        if (taskEntity.Status != (int)CrawlerTaskStatus.DeadLettered)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new(DeadLetterReplayStatus.OriginalTaskNotDeadLettered);
        }

        var originalTask = CrawlerTaskMapper.ToDomain(taskEntity);
        var replayTask = CrawlerTask.Create(
            originalTask.Payload,
            maxAttempts: originalTask.MaxAttempts,
            createdAt: now);

        db.Tasks.Add(CrawlerTaskMapper.ToEntity(replayTask));
        // AddDeadLetterAsync 可能刚在同一个 DbContext 中写入并仍在 Local 缓存；
        // 优先复用已跟踪实例，避免同键实体冲突，同时保留 FOR UPDATE 的锁语义。
        var trackedDeadLetter = db.DeadLetters.Local
            .SingleOrDefault(candidate => candidate.Id == deadLetter.Id);
        if (trackedDeadLetter is not null)
        {
            deadLetter = trackedDeadLetter;
        }
        else
        {
            db.DeadLetters.Attach(deadLetter);
        }

        deadLetter.ReplayTaskId = replayTask.Id;
        deadLetter.ReplayedAt = now;
        deadLetter.ReplayRequestedBy = command.RequestedBy;
        deadLetter.ReplayReason = command.ReplayReason;

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return new(DeadLetterReplayStatus.Replayed, replayTask.Id);
    }
}
