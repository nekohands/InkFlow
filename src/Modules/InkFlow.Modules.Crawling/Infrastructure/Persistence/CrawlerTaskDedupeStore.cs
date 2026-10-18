using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using InkFlow.BuildingBlocks.Persistence;
using InkFlow.Modules.Crawling.Application;
using InkFlow.Modules.Crawling.Domain;
using InkFlow.Modules.Sources.Domain;
using Microsoft.EntityFrameworkCore;

namespace InkFlow.Modules.Crawling.Infrastructure.Persistence;

/// <summary>
/// Transaction-scoped locks serialize scheduler dedupe checks for one stable
/// (source, capability, variable, value) key across API and Scheduler instances.
/// </summary>
internal static class CrawlerTaskAdvisoryLock
{
    private const int NamespaceKey = 1202;

    public static Task AcquireAsync(
        CrawlingDbContext db,
        string sourceId,
        SourceCapability capability,
        string variableName,
        string variableValue,
        CancellationToken cancellationToken)
    {
        var identity = Encoding.UTF8.GetBytes(
            $"{sourceId}\0{(int)capability}\0{variableName}\0{variableValue}");
        var hash = BinaryPrimitives.ReadInt32LittleEndian(SHA256.HashData(identity));

        return db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock({NamespaceKey}, {hash})",
            cancellationToken);
    }
}

/// <summary>
/// Owns the durable dedupe gate for crawler tasks, including its transaction
/// locks, collection-run guard and task-created outbox message.
/// </summary>
internal sealed class CrawlerTaskDedupeStore(
    CrawlingDbContext db,
    ITransactionalOutboxWriter outbox)
{
    public async Task<bool> HasActiveAsync(
        string sourceId,
        SourceCapability capability,
        CancellationToken cancellationToken = default)
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

    public Task<bool> HasConflictingAsync(
        string sourceId,
        SourceCapability capability,
        string variableName,
        string variableValue,
        CancellationToken cancellationToken = default) =>
        HasBlockingAsync(
            sourceId,
            capability,
            variableName,
            variableValue,
            cancellationToken);

    public async Task<bool> TryAddIfNoConflictingAsync(
        CrawlerTask task,
        string variableName,
        string variableValue,
        CancellationToken cancellationToken = default,
        bool ignoreDeadLettered = false)
    {
        ValidateDedupeTask(task, variableName, variableValue);

        await using var transaction = await db.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);
        var inserted = await TryAddIfNoConflictingInTransactionAsync(
                task,
                variableName,
                variableValue,
                cancellationToken,
                ignoreDeadLettered)
            .ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return inserted;
    }

    public async Task<bool> TryAddIfNoConflictingForCollectionRunAsync(
        CrawlerTask task,
        Guid runId,
        string variableName,
        string variableValue,
        CancellationToken cancellationToken = default,
        bool ignoreDeadLettered = false)
    {
        ValidateDedupeTask(task, variableName, variableValue);
        if (runId == Guid.Empty)
        {
            throw new ArgumentException("collection run ID must not be empty.", nameof(runId));
        }

        if (task.Payload.RunId != runId)
        {
            throw new ArgumentException(
                "the task must reference the requested collection run.",
                nameof(runId));
        }

        await using var transaction = await db.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);

        // Controls and this enqueue gate lock the same parent row. If a stop or
        // cancel commits first, the gate re-reads the durable state and rejects
        // the follow-up; if enqueue commits first, the later control observes it.
        var runEntity = await CrawlerParentRunLock.LockAsync(
                db,
                runId,
                cancellationToken,
                asNoTracking: true)
            .ConfigureAwait(false);
        if (runEntity is null || runEntity.Status is not (
                (int)CollectionRunStatus.Pending or
                (int)CollectionRunStatus.Running or
                (int)CollectionRunStatus.Paused))
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return false;
        }

        var inserted = await TryAddIfNoConflictingInTransactionAsync(
                task,
                variableName,
                variableValue,
                cancellationToken,
                ignoreDeadLettered)
            .ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return inserted;
    }

    private async Task<bool> TryAddIfNoConflictingInTransactionAsync(
        CrawlerTask task,
        string variableName,
        string variableValue,
        CancellationToken cancellationToken,
        bool ignoreDeadLettered)
    {
        await CrawlerTaskAdvisoryLock
            .AcquireAsync(
                db,
                task.Payload.SourceId,
                task.Payload.Capability,
                variableName,
                variableValue,
                cancellationToken)
            .ConfigureAwait(false);

        if (await HasBlockingAsync(
                task.Payload.SourceId,
                task.Payload.Capability,
                variableName,
                variableValue,
                cancellationToken,
                ignoreDeadLettered)
                .ConfigureAwait(false))
        {
            return false;
        }

        db.Tasks.Add(CrawlerTaskMapper.ToEntity(task));
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await outbox.EnqueueAsync(
                db,
                CrawlerIntegrationMessages.TaskCreated(task),
                cancellationToken)
            .ConfigureAwait(false);
        return true;
    }

    private static void ValidateDedupeTask(
        CrawlerTask task,
        string variableName,
        string variableValue)
    {
        ArgumentNullException.ThrowIfNull(task);
        if (string.IsNullOrWhiteSpace(variableName))
        {
            throw new ArgumentException("variable name must not be empty.", nameof(variableName));
        }

        if (!task.Payload.Variables.TryGetValue(variableName, out var taskVariableValue) ||
            taskVariableValue != variableValue)
        {
            throw new ArgumentException(
                "the task must contain the requested dedupe variable and value.",
                nameof(variableValue));
        }
    }

    public async Task<bool> HasBlockingAsync(
        string sourceId,
        SourceCapability capability,
        string variableName,
        string variableValue,
        CancellationToken cancellationToken = default,
        bool ignoreDeadLettered = false)
    {
        var blockingStatuses = new[]
        {
            (int)CrawlerTaskStatus.Pending,
            (int)CrawlerTaskStatus.Leased,
            (int)CrawlerTaskStatus.Running,
            (int)CrawlerTaskStatus.DeadLettered,
        };

        // Variables 为 jsonb 字典,EF 无法翻译取值比较;先按 (source, capability, 状态)
        // 服务端裁剪,再在内存中匹配变量。单来源单能力的任务量级有限。
        var candidates = await db.Tasks
            .Where(t => t.SourceId == sourceId &&
                        t.Capability == (int)capability &&
                        blockingStatuses.Contains(t.Status) &&
                        (ignoreDeadLettered ||
                         t.Status != (int)CrawlerTaskStatus.DeadLettered ||
                         !db.DeadLetters.Any(d => d.TaskId == t.Id && d.ReplayTaskId != null)))
            .Select(t => t.Variables)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return candidates.Any(variables =>
            variables.TryGetValue(variableName, out var value) && value == variableValue);
    }
}
