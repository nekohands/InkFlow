using System.Text.Json;
using InkFlow.Modules.Billing.Application;
using StackExchange.Redis;

namespace InkFlow.Modules.Billing.Infrastructure;

/// <summary>配额快照缓存仅用于读加速；配额准入永远以 PostgreSQL 事务结果为准。</summary>
public sealed class RedisQuotaSnapshotCache(IConnectionMultiplexer redis) : IQuotaSnapshotCache
{
    private const string KeyPrefix = "inkflow:quota:snapshot:";

    public async Task<QuotaSnapshot?> GetAsync(
        Guid userId,
        DateTimeOffset periodStart,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var value = await redis.GetDatabase()
                .StringGetAsync(Key(userId, periodStart))
                .ConfigureAwait(false);
            return value.HasValue
                ? JsonSerializer.Deserialize<QuotaSnapshot>(value.ToString())
                : null;
        }
        catch (RedisException)
        {
            return null;
        }
    }

    public async Task SetAsync(
        QuotaSnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var ttl = snapshot.PeriodEnd - DateTimeOffset.UtcNow;
            if (ttl <= TimeSpan.Zero)
            {
                return;
            }

            await redis.GetDatabase()
                .StringSetAsync(
                    Key(snapshot.UserId, snapshot.PeriodStart),
                    JsonSerializer.Serialize(snapshot),
                    ttl)
                .ConfigureAwait(false);
        }
        catch (RedisException)
        {
            // Cache 故障不能影响 PostgreSQL 已提交的配额事实。
        }
    }

    public async Task RemoveAsync(
        Guid userId,
        DateTimeOffset periodStart,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            await redis.GetDatabase().KeyDeleteAsync(Key(userId, periodStart)).ConfigureAwait(false);
        }
        catch (RedisException)
        {
        }
    }

    private static RedisKey Key(Guid userId, DateTimeOffset periodStart) =>
        $"{KeyPrefix}{userId:D}:{periodStart:yyyy-MM}";
}
