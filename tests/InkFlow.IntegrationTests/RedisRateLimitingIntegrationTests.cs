using InkFlow.Api;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using StackExchange.Redis;

namespace InkFlow.IntegrationTests;

/// <summary>
/// 真实 Redis 集成验证：两个独立连接上的 limiter 共享同一固定窗口配额。
/// 默认不启动外部服务；CI 在 Compose Redis 启动后通过环境变量显式运行本类。
/// </summary>
[TestClass]
public sealed class RedisRateLimitingIntegrationTests
{
    [TestMethod]
    public async Task Redis_Counter_Is_Shared_Across_Separate_Connections()
    {
        var connectionString = Environment.GetEnvironmentVariable("INKFLOW_REDIS_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Assert.Inconclusive(
                "set INKFLOW_REDIS_CONNECTION to run the real Redis integration check");
            return;
        }

        using var firstConnection = await ConnectionMultiplexer
            .ConnectAsync(connectionString)
            .ConfigureAwait(false);
        using var secondConnection = await ConnectionMultiplexer
            .ConnectAsync(connectionString)
            .ConfigureAwait(false);
        var key = $"inkflow:test:rate-limit:{Guid.NewGuid():N}";

        try
        {
            using var firstLimiter = new RedisFixedWindowRateLimiter(
                new RedisRateLimitCounter(firstConnection),
                key,
                permitLimit: 1,
                window: TimeSpan.FromSeconds(20),
                queueLimit: 0,
                NullLogger<RedisFixedWindowRateLimiter>.Instance);
            using var secondLimiter = new RedisFixedWindowRateLimiter(
                new RedisRateLimitCounter(secondConnection),
                key,
                permitLimit: 1,
                window: TimeSpan.FromSeconds(20),
                queueLimit: 0,
                NullLogger<RedisFixedWindowRateLimiter>.Instance);

            using var firstLease = await firstLimiter.AcquireAsync().ConfigureAwait(false);
            using var secondLease = await secondLimiter.AcquireAsync().ConfigureAwait(false);

            Assert.IsTrue(firstLease.IsAcquired);
            Assert.IsFalse(secondLease.IsAcquired);
            Assert.IsTrue(secondLease.TryGetMetadata(
                System.Threading.RateLimiting.MetadataName.RetryAfter,
                out TimeSpan retryAfter));
            Assert.IsTrue(retryAfter > TimeSpan.Zero);
        }
        finally
        {
            await firstConnection.GetDatabase().KeyDeleteAsync(key).ConfigureAwait(false);
        }
    }
}
