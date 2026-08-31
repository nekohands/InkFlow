using System.Reflection;
using InkFlow.Modules.Billing.Infrastructure;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using StackExchange.Redis;

namespace InkFlow.UnitTests;

[TestClass]
public sealed class QuotaSnapshotCacheTests
{
    [TestMethod]
    public async Task Corrupt_Redis_Snapshot_Is_Treated_As_Cache_Miss()
    {
        var cache = new RedisQuotaSnapshotCache(CreateConnection("{not-json"));

        var result = await cache.GetAsync(
            Guid.CreateVersion7(),
            new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero));

        Assert.IsNull(result, "A corrupt cache entry must fall back to PostgreSQL.");
    }

    private static IConnectionMultiplexer CreateConnection(string payload)
    {
        var database = DispatchProxy.Create<IDatabase, DatabaseProxy>();
        ((DatabaseProxy)(object)database).Payload = payload;

        var connection = DispatchProxy.Create<IConnectionMultiplexer, ConnectionProxy>();
        ((ConnectionProxy)(object)connection).Database = database;
        return connection;
    }

    private class ConnectionProxy : DispatchProxy
    {
        public IDatabase Database { get; set; } = null!;

        protected override object Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == nameof(IConnectionMultiplexer.GetDatabase))
            {
                return Database;
            }

            throw new NotSupportedException($"Unexpected Redis connection call: {targetMethod?.Name}");
        }
    }

    private class DatabaseProxy : DispatchProxy
    {
        public RedisValue Payload { get; set; }

        protected override object Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == nameof(IDatabase.StringGetAsync))
            {
                return Task.FromResult(Payload);
            }

            throw new NotSupportedException($"Unexpected Redis database call: {targetMethod?.Name}");
        }
    }
}
