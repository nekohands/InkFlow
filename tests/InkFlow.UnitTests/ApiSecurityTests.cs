using System.Net;
using System.Security.Claims;
using System.Threading.RateLimiting;
using InkFlow.Api;
using InkFlow.BuildingBlocks.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace InkFlow.UnitTests;

[TestClass]
public sealed class ApiSecurityTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 27, 19, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public async Task Fixed_Window_Limits_Anonymous_Client_By_Remote_Address()
    {
        var context = CreateContext("203.0.113.10");
        var partition = ApiRateLimitPolicies.CreatePartition(
            context,
            ApiRateLimitPolicies.PublicPolicyName,
            permitLimit: 1,
            window: TimeSpan.FromMinutes(1),
            queueLimit: 0);
        using var limiter = partition.Factory(partition.PartitionKey);

        using var first = await limiter.AcquireAsync(1);
        using var second = await limiter.AcquireAsync(1);

        Assert.IsTrue(first.IsAcquired);
        Assert.IsFalse(second.IsAcquired);
    }

    [TestMethod]
    public void Anonymous_Rate_Limit_Key_Does_Not_Trust_Forwarded_Headers_Or_Raw_Tokens()
    {
        var first = CreateContext("203.0.113.10");
        first.Request.Headers["X-Forwarded-For"] = "198.51.100.20";
        first.Request.Headers.Authorization = "Bearer token-a";

        var second = CreateContext("203.0.113.10");
        second.Request.Headers.Authorization = "Bearer token-b";

        Assert.AreEqual(
            ApiRateLimitPolicies.ResolveClientKey(first),
            ApiRateLimitPolicies.ResolveClientKey(second));
    }

    [TestMethod]
    public void Authenticated_Rate_Limit_Key_Uses_Hashed_Subject()
    {
        var first = CreateContext("203.0.113.10");
        first.User = Principal("user-a");
        var second = CreateContext("198.51.100.20");
        second.User = Principal("user-a");

        var key = ApiRateLimitPolicies.ResolveClientKey(first);

        Assert.AreEqual(key, ApiRateLimitPolicies.ResolveClientKey(second));
        StringAssert.StartsWith(key, "principal:");
        Assert.IsFalse(key.Contains("user-a", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Rate_Limit_Options_Reject_Unbounded_Configuration()
    {
        var options = new ApiRateLimitOptions { PublicPermitLimit = 0 };

        Assert.ThrowsExactly<InvalidOperationException>(() => options.Validate());
    }

    [TestMethod]
    public void Rate_Limit_Options_Read_Redis_Connection_From_Configuration()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Redis"] = "redis.example:6379,abortConnect=false",
                ["RateLimiting:RedisKeyPrefix"] = "inkflow:test-rate-limit",
            })
            .Build();

        var options = ApiRateLimitOptions.FromConfiguration(configuration);

        Assert.AreEqual("redis.example:6379,abortConnect=false", options.RedisConnectionString);
        Assert.AreEqual("inkflow:test-rate-limit", options.RedisKeyPrefix);
    }

    [TestMethod]
    public async Task Redis_Rate_Limit_Is_Shared_By_Separate_Limiter_Instances()
    {
        var counter = new InMemoryDistributedRateLimitCounter();
        using var first = CreateRedisLimiter(counter, "shared-key");
        using var second = CreateRedisLimiter(counter, "shared-key");

        using var firstLease = await first.AcquireAsync();
        using var secondLease = await second.AcquireAsync();

        Assert.IsTrue(firstLease.IsAcquired);
        Assert.IsFalse(secondLease.IsAcquired);
        Assert.IsTrue(secondLease.TryGetMetadata(
            MetadataName.RetryAfter,
            out TimeSpan retryAfter));
        Assert.IsTrue(retryAfter > TimeSpan.Zero);
    }

    [TestMethod]
    public async Task Redis_Rate_Limit_Uses_Bounded_Local_Fallback_When_Store_Is_Unavailable()
    {
        using var limiter = CreateRedisLimiter(new UnavailableRateLimitCounter(), "fallback-key");

        using var firstLease = await limiter.AcquireAsync();
        using var secondLease = await limiter.AcquireAsync();

        Assert.IsTrue(firstLease.IsAcquired);
        Assert.IsFalse(secondLease.IsAcquired);
    }

    [TestMethod]
    public void Redis_Rate_Limit_Key_Does_Not_Expose_Client_Identity()
    {
        const string partitionKey = "api-public:ip:203.0.113.10";

        var key = RedisRateLimiterFactory.BuildRedisKey(
            "inkflow:rate-limit",
            ApiRateLimitPolicies.PublicPolicyName,
            partitionKey);

        Assert.IsFalse(key.Contains(partitionKey, StringComparison.Ordinal));
        StringAssert.StartsWith(key, "inkflow:rate-limit:api-public:");
    }

    [TestMethod]
    public void Audit_Event_Is_Bounded_And_Removes_Log_Line_Breaks()
    {
        var auditEvent = AuditEvent.Create(
            action: "GET",
            resource: "/api/v1/books",
            outcome: "success",
            statusCode: 200,
            occurredAt: T0,
            reason: " upstream\r\nretry ");

        Assert.AreEqual("upstream  retry", auditEvent.Reason);
        Assert.AreNotEqual(Guid.Empty, auditEvent.Id);
    }

    [TestMethod]
    public async Task Request_Audit_Records_Status_Without_Query_String()
    {
        var context = CreateContext("203.0.113.10");
        context.Request.Method = "GET";
        context.Request.Path = "/api/v1/books";
        context.Request.QueryString = new QueryString("?q=secret-search-term");
        var sink = new InMemoryAuditSink();
        var middleware = new RequestAuditMiddleware(
            next: httpContext =>
            {
                httpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                return Task.CompletedTask;
            },
            NullLogger<RequestAuditMiddleware>.Instance);

        await middleware.InvokeAsync(context, sink, new FixedClock(T0));

        var auditEvent = sink.Events.Single();
        Assert.AreEqual(429, auditEvent.StatusCode);
        Assert.AreEqual("client_error", auditEvent.Outcome);
        Assert.AreEqual("/api/v1/books", auditEvent.Resource);
        Assert.IsFalse(auditEvent.Resource.Contains("secret-search-term", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task Request_Audit_Skips_Health_Endpoint()
    {
        var context = CreateContext("203.0.113.10");
        context.Request.Path = "/health";
        var sink = new InMemoryAuditSink();
        var middleware = new RequestAuditMiddleware(
            _ => Task.CompletedTask,
            NullLogger<RequestAuditMiddleware>.Instance);

        await middleware.InvokeAsync(context, sink, new FixedClock(T0));

        Assert.AreEqual(0, sink.Events.Count);
    }

    private static DefaultHttpContext CreateContext(string address)
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse(address);
        return context;
    }

    private static ClaimsPrincipal Principal(string subject) => new(
        new ClaimsIdentity(
            [new Claim("sub", subject)],
            authenticationType: "test"));

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class InMemoryAuditSink : IAuditEventSink
    {
        public List<AuditEvent> Events { get; } = [];

        public ValueTask AppendAsync(
            AuditEvent auditEvent,
            CancellationToken cancellationToken = default)
        {
            Events.Add(auditEvent);
            return ValueTask.CompletedTask;
        }
    }

    private static RedisFixedWindowRateLimiter CreateRedisLimiter(
        IDistributedRateLimitCounter counter,
        string key) =>
        new(
            counter,
            key,
            permitLimit: 1,
            window: TimeSpan.FromMinutes(1),
            queueLimit: 0,
            NullLogger<RedisFixedWindowRateLimiter>.Instance);

    private sealed class InMemoryDistributedRateLimitCounter : IDistributedRateLimitCounter
    {
        private readonly Dictionary<string, (long Count, DateTimeOffset ExpiresAt)> _counts = [];
        private readonly object _gate = new();

        public RateLimitCounterResult TryAcquire(
            string key,
            int permitCount,
            int permitLimit,
            TimeSpan window)
        {
            lock (_gate)
            {
                var now = DateTimeOffset.UtcNow;
                if (_counts.TryGetValue(key, out var current) && current.ExpiresAt <= now)
                {
                    _counts.Remove(key);
                    current = default;
                }

                var retryAfter = current.ExpiresAt > now
                    ? current.ExpiresAt - now
                    : window;
                if (current.Count + permitCount > permitLimit)
                {
                    return new RateLimitCounterResult(false, current.Count, retryAfter);
                }

                var next = current with
                {
                    Count = current.Count + permitCount,
                    ExpiresAt = current.ExpiresAt > now ? current.ExpiresAt : now + window,
                };
                _counts[key] = next;
                return new RateLimitCounterResult(true, next.Count, next.ExpiresAt - now);
            }
        }

        public ValueTask<RateLimitCounterResult> TryAcquireAsync(
            string key,
            int permitCount,
            int permitLimit,
            TimeSpan window,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(TryAcquire(key, permitCount, permitLimit, window));
    }

    private sealed class UnavailableRateLimitCounter : IDistributedRateLimitCounter
    {
        public RateLimitCounterResult TryAcquire(
            string key,
            int permitCount,
            int permitLimit,
            TimeSpan window) =>
            throw new RateLimitStoreUnavailableException(new IOException("test outage"));

        public ValueTask<RateLimitCounterResult> TryAcquireAsync(
            string key,
            int permitCount,
            int permitLimit,
            TimeSpan window,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromException<RateLimitCounterResult>(
                new RateLimitStoreUnavailableException(new IOException("test outage")));
    }
}
