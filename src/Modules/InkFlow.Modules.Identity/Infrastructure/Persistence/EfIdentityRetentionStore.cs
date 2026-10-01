using Microsoft.EntityFrameworkCore;
using InkFlow.Modules.Identity.Application;

namespace InkFlow.Modules.Identity.Infrastructure.Persistence;

/// <summary>
/// PostgreSQL Identity 保留实现：每个会话在单独事务内按“子表优先”的顺序删除，
/// 并用 SKIP LOCKED 避免与认证写入互相阻塞。
///
/// 删除范围只由一对条件决定，两条都必须成立：
///   1. 行的 <c>ExpiresAt</c> 已早于 cutoff（会话事实整体过期）；
///   2. 行处于终态 —— 会话 <c>RevokedAt IS NOT NULL</c>，访问令牌 <c>RevokedAt IS NOT NULL</c>。
///
/// 条件 2 是必需的：撤销时间晚于过期时间时（改密/重放吊销发生在过期之后），
/// 任何未经确认的过期行都必须保留到撤销本身也超出宽限期为止，否则会删掉
/// 尚未处理的吊销事实。会话删除必须带 NOT EXISTS 护栏：<c>access_tokens → sessions</c>
/// 外键是 ON DELETE CASCADE，若无护栏，删除会话会把其下仍未终态的令牌一并级联删除。
/// 这也是子表优先删除的原因：先删令牌再删会话，保证事务内不会出现悬挂的令牌引用。
/// </summary>
public sealed class EfIdentityRetentionStore(IdentityDbContext db) : IIdentityRetentionStore
{
    public async Task<IdentityRetentionResult> DeleteExpiredBatchAsync(
        DateTimeOffset sessionCutoff,
        DateTimeOffset accessTokenCutoff,
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        ValidateCutoff(sessionCutoff, nameof(sessionCutoff));
        ValidateCutoff(accessTokenCutoff, nameof(accessTokenCutoff));
        ValidateBatchSize(batchSize);

        var sessionCutoffUtc = sessionCutoff.ToUniversalTime();
        var accessTokenCutoffUtc = accessTokenCutoff.ToUniversalTime();

        // SKIP LOCKED 的候选集在一条语句内取完，避免每批多一次往返。
        var candidateSessionIds = await db.Database
            .SqlQuery<Guid>($"""
                SELECT "Id" AS "Value"
                  FROM "identity"."sessions"
                 WHERE "RevokedAt" IS NOT NULL
                   AND "ExpiresAt" < {sessionCutoffUtc}
                 ORDER BY "ExpiresAt", "Id"
                 LIMIT {batchSize}
                 FOR UPDATE SKIP LOCKED
                """)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (candidateSessionIds.Count == 0)
        {
            return new IdentityRetentionResult(0, 0);
        }

        var deletedSessions = 0;
        var deletedAccessTokens = 0;

        foreach (var sessionId in candidateSessionIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await using var transaction = await db.Database
                .BeginTransactionAsync(cancellationToken)
                .ConfigureAwait(false);

            // 子表优先：access_tokens 对 sessions 有外键 FK_access_tokens_sessions_SessionId，
            // 先删令牌才能删会话，否则事务内会出现悬挂引用。
            deletedAccessTokens += await db.Database.ExecuteSqlInterpolatedAsync($"""
                DELETE FROM "identity"."access_tokens"
                 WHERE "SessionId" = {sessionId}
                   AND "RevokedAt" IS NOT NULL
                   AND "ExpiresAt" < {accessTokenCutoffUtc};
                """, cancellationToken).ConfigureAwait(false);

            // 同一事务内复查会话谓词，保证“已确认为终态”的判断不被并发认证写操作推翻；
            // NOT EXISTS 护栏确保仍存在未终态令牌的会话不会被（级联）删除。
            deletedSessions += await db.Database.ExecuteSqlInterpolatedAsync($"""
                DELETE FROM "identity"."sessions" AS s
                 WHERE s."Id" = {sessionId}
                   AND s."RevokedAt" IS NOT NULL
                   AND s."ExpiresAt" < {sessionCutoffUtc}
                   AND NOT EXISTS (
                       SELECT 1 FROM "identity"."access_tokens" AS t
                        WHERE t."SessionId" = s."Id"
                          AND (t."RevokedAt" IS NULL OR t."ExpiresAt" >= {accessTokenCutoffUtc}));
                """, cancellationToken).ConfigureAwait(false);

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }

        db.ChangeTracker.Clear();
        return new IdentityRetentionResult(deletedSessions, deletedAccessTokens);
    }

    private static void ValidateCutoff(DateTimeOffset cutoff, string parameterName)
    {
        if (cutoff == DateTimeOffset.MinValue || cutoff == DateTimeOffset.MaxValue)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }

    private static void ValidateBatchSize(int batchSize)
    {
        if (batchSize is < 1 or > 1_000)
        {
            throw new ArgumentOutOfRangeException(nameof(batchSize));
        }
    }
}
