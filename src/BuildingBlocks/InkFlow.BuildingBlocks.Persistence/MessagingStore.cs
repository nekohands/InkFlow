using InkFlow.BuildingBlocks.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace InkFlow.BuildingBlocks.Persistence;

/// <summary>
/// 让模块 DbContext 在自己的数据库事务中追加 Outbox 行。
/// 调用方必须先开启事务，并在业务数据与消息都写入后再提交。
/// </summary>
public interface ITransactionalOutboxWriter
{
    Task EnqueueAsync(
        DbContext transactionContext,
        IntegrationMessage message,
        CancellationToken cancellationToken = default);
}

public sealed class EfTransactionalOutboxWriter : ITransactionalOutboxWriter
{
    public async Task EnqueueAsync(
        DbContext transactionContext,
        IntegrationMessage message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(transactionContext);
        ArgumentNullException.ThrowIfNull(message);
        if (transactionContext.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException(
                "transactional outbox requires an active database transaction.");
        }

        await MessagingSql.InsertOutboxAsync(
                transactionContext.Database,
                message,
                cancellationToken)
            .ConfigureAwait(false);
    }
}

/// <summary>PostgreSQL Outbox/Inbox 的 EF 持久化实现。</summary>
public sealed class EfMessagingMessageStore(MessagingDbContext db)
    : IOutboxStore, IInboxStore, IMessageRetentionStore
{
    public async Task EnqueueAsync(
        IntegrationMessage message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        await MessagingSql.InsertOutboxAsync(db.Database, message, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<OutboxMessageRecord>> ClaimBatchAsync(
        string owner,
        DateTimeOffset now,
        TimeSpan leaseDuration,
        int limit,
        CancellationToken cancellationToken = default)
    {
        ValidateOwner(owner);
        ValidateLease(leaseDuration);
        ValidateLimit(limit);
        now = now.ToUniversalTime();

        await using var transaction = await db.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);

        var messages = await db.OutboxMessages
            .FromSqlInterpolated($"""
                SELECT *
                  FROM "messaging"."outbox_messages"
                 WHERE "ProcessedAt" IS NULL
                   AND "AvailableAt" <= {now}
                   AND ("LockedUntil" IS NULL OR "LockedUntil" <= {now})
                 ORDER BY "OccurredAt", "Id"
                 LIMIT {limit}
                 FOR UPDATE SKIP LOCKED
                """)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var lockedUntil = now + leaseDuration;
        foreach (var message in messages)
        {
            message.LockOwner = owner;
            message.LockedUntil = lockedUntil;
            message.AttemptCount = Increment(message.AttemptCount);
            message.LastError = null;
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

        return messages.Select(ToRecord).ToList();
    }

    public async Task MarkPublishedAsync(
        Guid messageId,
        string owner,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        ValidateOwner(owner);
        ValidateId(messageId);
        now = now.ToUniversalTime();

        await using var transaction = await db.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);
        var message = await GetOutboxForUpdateAsync(messageId, cancellationToken)
            .ConfigureAwait(false);
        if (message.ProcessedAt is not null)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        EnsureLease(message.LockOwner, message.LockedUntil, owner, now, "publish");
        message.ProcessedAt = now;
        message.LockOwner = null;
        message.LockedUntil = null;
        message.LastError = null;
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task MarkFailedAsync(
        Guid messageId,
        string owner,
        DateTimeOffset now,
        DateTimeOffset availableAt,
        string failureCode,
        CancellationToken cancellationToken = default)
    {
        ValidateOwner(owner);
        ValidateId(messageId);
        var normalizedFailure = ValidateFailureCode(failureCode);
        now = now.ToUniversalTime();
        availableAt = availableAt.ToUniversalTime();
        if (availableAt < now)
        {
            throw new ArgumentException(
                "outbox retry time must not be before now.",
                nameof(availableAt));
        }

        await using var transaction = await db.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);
        var message = await GetOutboxForUpdateAsync(messageId, cancellationToken)
            .ConfigureAwait(false);
        if (message.ProcessedAt is not null)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        EnsureLease(message.LockOwner, message.LockedUntil, owner, now, "fail");
        message.AvailableAt = availableAt;
        message.LockOwner = null;
        message.LockedUntil = null;
        message.LastError = normalizedFailure;
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<MessageRetentionBatchResult> DeleteProcessedBatchAsync(
        DateTimeOffset outboxCutoff,
        DateTimeOffset inboxCutoff,
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        ValidateCutoff(outboxCutoff, nameof(outboxCutoff));
        ValidateCutoff(inboxCutoff, nameof(inboxCutoff));
        ValidateRetentionLimit(batchSize);

        await using var transaction = await db.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);
        var outboxDeleted = await DeleteOutboxBatchAsync(
                db.Database,
                outboxCutoff.ToUniversalTime(),
                batchSize,
                cancellationToken)
            .ConfigureAwait(false);
        var inboxDeleted = await DeleteInboxBatchAsync(
                db.Database,
                inboxCutoff.ToUniversalTime(),
                batchSize,
                cancellationToken)
            .ConfigureAwait(false);
        db.ChangeTracker.Clear();
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

        return new(outboxDeleted, inboxDeleted);
    }

    public async Task<InboxClaimResult> TryClaimAsync(
        IntegrationMessage message,
        string owner,
        DateTimeOffset now,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        ValidateOwner(owner);
        ValidateLease(leaseDuration);
        now = now.ToUniversalTime();

        await using var transaction = await db.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);
        await MessagingSql.InsertInboxAsync(db.Database, message, now, cancellationToken)
            .ConfigureAwait(false);

        var storedMessages = await db.InboxMessages
            .FromSqlInterpolated($"""
                SELECT *
                  FROM "messaging"."inbox_messages"
                 WHERE "Id" = {message.Id}
                 FOR UPDATE
                """)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var stored = storedMessages.Single();

        EnsureMessageMatches(stored.MessageType, stored.PayloadHash, message);
        if (stored.ProcessedAt is not null)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new(message.Id, InboxClaimStatus.AlreadyProcessed, stored.AttemptCount);
        }

        if (stored.LockedUntil > now)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new(message.Id, InboxClaimStatus.AlreadyInProgress, stored.AttemptCount);
        }

        stored.LockOwner = owner;
        stored.LockedUntil = now + leaseDuration;
        stored.AttemptCount = Increment(stored.AttemptCount);
        stored.LastError = null;
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return new(message.Id, InboxClaimStatus.Claimed, stored.AttemptCount);
    }

    public async Task MarkProcessedAsync(
        Guid messageId,
        string owner,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        ValidateOwner(owner);
        ValidateId(messageId);
        now = now.ToUniversalTime();

        await using var transaction = await db.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);
        var message = await GetInboxForUpdateAsync(messageId, cancellationToken)
            .ConfigureAwait(false);
        if (message.ProcessedAt is not null)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        EnsureLease(message.LockOwner, message.LockedUntil, owner, now, "process");
        message.ProcessedAt = now;
        message.LockOwner = null;
        message.LockedUntil = null;
        message.LastError = null;
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task MarkFailedAsync(
        Guid messageId,
        string owner,
        DateTimeOffset now,
        string failureCode,
        CancellationToken cancellationToken = default)
    {
        ValidateOwner(owner);
        ValidateId(messageId);
        var normalizedFailure = ValidateFailureCode(failureCode);
        now = now.ToUniversalTime();

        await using var transaction = await db.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);
        var message = await GetInboxForUpdateAsync(messageId, cancellationToken)
            .ConfigureAwait(false);
        if (message.ProcessedAt is not null)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        EnsureLease(message.LockOwner, message.LockedUntil, owner, now, "fail");
        message.LockOwner = null;
        message.LockedUntil = null;
        message.LastError = normalizedFailure;
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<OutboxMessageEntity> GetOutboxForUpdateAsync(
        Guid messageId,
        CancellationToken cancellationToken)
    {
        var messages = await db.OutboxMessages
            .FromSqlInterpolated($"""
                SELECT *
                  FROM "messaging"."outbox_messages"
                 WHERE "Id" = {messageId}
                 FOR UPDATE
                """)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return messages.Single();
    }

    private async Task<InboxMessageEntity> GetInboxForUpdateAsync(
        Guid messageId,
        CancellationToken cancellationToken)
    {
        var messages = await db.InboxMessages
            .FromSqlInterpolated($"""
                SELECT *
                  FROM "messaging"."inbox_messages"
                 WHERE "Id" = {messageId}
                 FOR UPDATE
                """)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return messages.Single();
    }

    private static OutboxMessageRecord ToRecord(OutboxMessageEntity message) =>
        new(
            message.Id,
            message.MessageType,
            message.OccurredAt,
            message.AvailableAt,
            message.Payload,
            message.PayloadHash,
            message.TraceId,
            message.AttemptCount,
            message.LockOwner,
            message.LockedUntil,
            message.ProcessedAt,
            message.LastError);

    private static void EnsureMessageMatches(
        string storedType,
        string storedPayloadHash,
        IntegrationMessage message)
    {
        if (!string.Equals(storedType, message.MessageType, StringComparison.Ordinal) ||
            !string.Equals(storedPayloadHash, message.PayloadHash, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"inbox message {message.Id} does not match the stored message identity.");
        }
    }

    private static void EnsureLease(
        string? lockOwner,
        DateTimeOffset? lockedUntil,
        string owner,
        DateTimeOffset now,
        string operation)
    {
        if (!string.Equals(lockOwner, owner, StringComparison.Ordinal) ||
            lockedUntil is null || lockedUntil <= now)
        {
            throw new InvalidOperationException(
                $"message lease was lost before {operation} acknowledgement.");
        }
    }

    private static void ValidateOwner(string owner)
    {
        if (string.IsNullOrWhiteSpace(owner) || owner.Trim().Length > 128 ||
            owner.Any(char.IsControl))
        {
            throw new ArgumentException("message owner is invalid.", nameof(owner));
        }
    }

    private static void ValidateLease(TimeSpan leaseDuration)
    {
        if (leaseDuration <= TimeSpan.Zero || leaseDuration > TimeSpan.FromHours(24))
        {
            throw new ArgumentOutOfRangeException(nameof(leaseDuration));
        }
    }

    private static void ValidateLimit(int limit)
    {
        if (limit is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(limit));
        }
    }

    private static void ValidateRetentionLimit(int limit)
    {
        if (limit is < 1 or > 1_000)
        {
            throw new ArgumentOutOfRangeException(nameof(limit));
        }
    }

    private static void ValidateId(Guid id)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("message ID must not be empty.", nameof(id));
        }
    }

    private static void ValidateCutoff(DateTimeOffset cutoff, string parameterName)
    {
        if (cutoff == DateTimeOffset.MinValue || cutoff == DateTimeOffset.MaxValue)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }

    private static string ValidateFailureCode(string failureCode)
    {
        if (string.IsNullOrWhiteSpace(failureCode))
        {
            throw new ArgumentException("failure code must not be empty.", nameof(failureCode));
        }

        var normalized = failureCode.Trim();
        if (normalized.Length > 128 || normalized.Any(char.IsControl))
        {
            throw new ArgumentException("failure code is invalid.", nameof(failureCode));
        }

        return normalized;
    }

    private static int Increment(int value) => value == int.MaxValue ? int.MaxValue : value + 1;

    private static Task<int> DeleteOutboxBatchAsync(
        DatabaseFacade database,
        DateTimeOffset cutoff,
        int batchSize,
        CancellationToken cancellationToken) =>
        database.ExecuteSqlInterpolatedAsync($"""
            WITH candidates AS (
                SELECT "Id"
                  FROM "messaging"."outbox_messages"
                 WHERE "ProcessedAt" IS NOT NULL
                   AND "ProcessedAt" < {cutoff}
                 ORDER BY "ProcessedAt", "Id"
                 LIMIT {batchSize}
                 FOR UPDATE SKIP LOCKED
            )
            DELETE FROM "messaging"."outbox_messages" AS target
             USING candidates
             WHERE target."Id" = candidates."Id";
            """, cancellationToken);

    private static Task<int> DeleteInboxBatchAsync(
        DatabaseFacade database,
        DateTimeOffset cutoff,
        int batchSize,
        CancellationToken cancellationToken) =>
        database.ExecuteSqlInterpolatedAsync($"""
            WITH candidates AS (
                SELECT "Id"
                  FROM "messaging"."inbox_messages"
                 WHERE "ProcessedAt" IS NOT NULL
                   AND "ProcessedAt" < {cutoff}
                 ORDER BY "ProcessedAt", "Id"
                 LIMIT {batchSize}
                 FOR UPDATE SKIP LOCKED
            )
            DELETE FROM "messaging"."inbox_messages" AS target
             USING candidates
             WHERE target."Id" = candidates."Id";
            """, cancellationToken);
}

internal static class MessagingSql
{
    public static async Task InsertOutboxAsync(
        DatabaseFacade database,
        IntegrationMessage message,
        CancellationToken cancellationToken)
    {
        var affected = await database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "messaging"."outbox_messages" AS target
                ("Id", "MessageType", "OccurredAt", "AvailableAt", "Payload",
                 "PayloadHash", "TraceId", "AttemptCount", "LockOwner", "LockedUntil",
                 "ProcessedAt", "LastError")
            VALUES
                ({message.Id}, {message.MessageType}, {message.OccurredAt}, {message.OccurredAt},
                 {message.Payload}::jsonb, {message.PayloadHash}, {message.TraceId}, 0,
                 NULL, NULL, NULL, NULL)
            ON CONFLICT ("Id") DO UPDATE
                SET "Id" = EXCLUDED."Id"
              WHERE target."MessageType" = EXCLUDED."MessageType"
                AND target."PayloadHash" = EXCLUDED."PayloadHash";
            """, cancellationToken).ConfigureAwait(false);
        if (affected == 0)
        {
            throw new InvalidOperationException(
                $"outbox message {message.Id} does not match the stored message identity.");
        }
    }

    public static async Task InsertInboxAsync(
        DatabaseFacade database,
        IntegrationMessage message,
        DateTimeOffset receivedAt,
        CancellationToken cancellationToken)
    {
        var affected = await database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "messaging"."inbox_messages" AS target
                ("Id", "MessageType", "Payload", "PayloadHash", "ReceivedAt", "AttemptCount",
                 "LockOwner", "LockedUntil", "ProcessedAt", "LastError")
            VALUES
                ({message.Id}, {message.MessageType}, {message.Payload}::jsonb,
                 {message.PayloadHash}, {receivedAt}, 0, NULL, NULL, NULL, NULL)
            ON CONFLICT ("Id") DO UPDATE
                SET "Id" = EXCLUDED."Id"
              WHERE target."MessageType" = EXCLUDED."MessageType"
                AND target."PayloadHash" = EXCLUDED."PayloadHash";
            """, cancellationToken).ConfigureAwait(false);
        if (affected == 0)
        {
            throw new InvalidOperationException(
                $"inbox message {message.Id} does not match the stored message identity.");
        }
    }
}
