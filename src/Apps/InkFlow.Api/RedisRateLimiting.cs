using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace InkFlow.Api;

/// <summary>
/// The result of one atomic distributed rate-limit counter operation.
/// </summary>
public sealed record RateLimitCounterResult(
    bool IsAcquired,
    long CurrentCount,
    TimeSpan RetryAfter);

/// <summary>
/// Storage seam for the distributed counter. It keeps the ASP.NET rate-limit
/// policy independent from the concrete Redis client and makes the policy
/// testable without a network dependency.
/// </summary>
public interface IDistributedRateLimitCounter
{
    RateLimitCounterResult TryAcquire(
        string key,
        int permitCount,
        int permitLimit,
        TimeSpan window);

    ValueTask<RateLimitCounterResult> TryAcquireAsync(
        string key,
        int permitCount,
        int permitLimit,
        TimeSpan window,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Signals that the distributed counter cannot be reached. The policy uses a
/// bounded local limiter for this case, so a Redis outage never becomes an
/// unbounded pass-through.
/// </summary>
public sealed class RateLimitStoreUnavailableException : Exception
{
    public RateLimitStoreUnavailableException(Exception innerException)
        : base("The distributed rate-limit store is unavailable.", innerException)
    {
    }
}

/// <summary>
/// Redis implementation of an atomic fixed-window counter. The Lua script
/// performs read/check/increment/expiry as one server-side operation, which
/// keeps two API instances on the same global quota.
/// </summary>
public sealed class RedisRateLimitCounter(
    IConnectionMultiplexer connection,
    IRateLimitStoreHealthRecorder? health = null)
    : IDistributedRateLimitCounter
{
    private const string AcquireScript = """
        local current = redis.call('GET', KEYS[1])
        if not current then
            current = 0
        else
            current = tonumber(current)
        end

        local requested = tonumber(ARGV[1])
        local limit = tonumber(ARGV[3])
        local ttl = redis.call('PTTL', KEYS[1])
        if ttl < 0 then
            ttl = tonumber(ARGV[2])
            if current > 0 then
                redis.call('PEXPIRE', KEYS[1], ARGV[2])
            end
        end

        if requested > limit or current + requested > limit then
            return {0, current, ttl}
        end

        current = redis.call('INCRBY', KEYS[1], requested)
        if current == requested then
            redis.call('PEXPIRE', KEYS[1], ARGV[2])
            ttl = tonumber(ARGV[2])
        else
            ttl = redis.call('PTTL', KEYS[1])
        end

        return {1, current, ttl}
        """;

    private readonly IDatabase _database = connection.GetDatabase();
    private readonly IRateLimitStoreHealthRecorder? _health = health;

    public RateLimitCounterResult TryAcquire(
        string key,
        int permitCount,
        int permitLimit,
        TimeSpan window)
    {
        ValidateArguments(key, permitCount, permitLimit, window);

        try
        {
            var result = _database.ScriptEvaluate(
                AcquireScript,
                [new RedisKey(key)],
                CreateArguments(permitCount, permitLimit, window),
                CommandFlags.DemandMaster);
            var parsed = ParseResult(result);
            _health?.RecordSuccess();
            return parsed;
        }
        catch (Exception exception)
        {
            _health?.RecordFailure();
            throw new RateLimitStoreUnavailableException(exception);
        }
    }

    public async ValueTask<RateLimitCounterResult> TryAcquireAsync(
        string key,
        int permitCount,
        int permitLimit,
        TimeSpan window,
        CancellationToken cancellationToken = default)
    {
        ValidateArguments(key, permitCount, permitLimit, window);
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var result = await _database.ScriptEvaluateAsync(
                    AcquireScript,
                    [new RedisKey(key)],
                    CreateArguments(permitCount, permitLimit, window),
                    CommandFlags.DemandMaster)
                .WaitAsync(cancellationToken)
                .ConfigureAwait(false);
            var parsed = ParseResult(result);
            _health?.RecordSuccess();
            return parsed;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _health?.RecordFailure();
            throw new RateLimitStoreUnavailableException(exception);
        }
    }

    private static RedisValue[] CreateArguments(
        int permitCount,
        int permitLimit,
        TimeSpan window) =>
    [
        permitCount,
        checked((long)Math.Ceiling(window.TotalMilliseconds)),
        permitLimit,
    ];

    private static RateLimitCounterResult ParseResult(RedisResult result)
    {
        var values = (RedisResult[]?)result;
        if (values is null || values.Length < 3)
        {
            throw new InvalidOperationException("Redis rate-limit script returned an invalid result.");
        }

        var acquired = ParseLong(values[0]) == 1;
        var currentCount = ParseLong(values[1]);
        var retryAfterMilliseconds = Math.Max(0, ParseLong(values[2]));
        return new RateLimitCounterResult(
            acquired,
            currentCount,
            TimeSpan.FromMilliseconds(retryAfterMilliseconds));
    }

    private static long ParseLong(RedisResult value) =>
        long.Parse(value.ToString(), CultureInfo.InvariantCulture);

    private static void ValidateArguments(
        string key,
        int permitCount,
        int permitLimit,
        TimeSpan window)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("rate-limit key must not be empty.", nameof(key));
        }

        if (permitCount < 1 || permitLimit < 1 || window <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(permitCount),
                "permit count and limit must be positive and the window must be positive.");
        }
    }
}

/// <summary>
/// ASP.NET RateLimiter adapter backed by the shared Redis counter. Redis
/// failures use the same finite permit/window settings in a local limiter and
/// emit one recovery-aware log transition per partition.
/// </summary>
public sealed class RedisFixedWindowRateLimiter : RateLimiter
{
    private readonly IDistributedRateLimitCounter _counter;
    private readonly string _key;
    private readonly int _permitLimit;
    private readonly TimeSpan _window;
    private readonly int _queueLimit;
    private readonly FixedWindowRateLimiter _localFallback;
    private readonly ILogger<RedisFixedWindowRateLimiter> _logger;
    private int _fallbackActive;
    private int _queuedPermits;

    public RedisFixedWindowRateLimiter(
        IDistributedRateLimitCounter counter,
        string key,
        int permitLimit,
        TimeSpan window,
        int queueLimit,
        ILogger<RedisFixedWindowRateLimiter> logger)
    {
        ArgumentNullException.ThrowIfNull(counter);
        ArgumentNullException.ThrowIfNull(logger);
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("rate-limit key must not be empty.", nameof(key));
        }

        if (permitLimit < 1 || window <= TimeSpan.Zero || queueLimit < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(permitLimit),
                "rate-limit settings must be positive and queue must not be negative.");
        }

        _counter = counter;
        _key = key;
        _permitLimit = permitLimit;
        _window = window;
        _queueLimit = queueLimit;
        _logger = logger;
        _localFallback = new FixedWindowRateLimiter(
            new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = permitLimit,
                Window = window,
                QueueLimit = queueLimit,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            });
    }

    public override TimeSpan? IdleDuration => null;

    public override RateLimiterStatistics? GetStatistics() => null;

    protected override RateLimitLease AttemptAcquireCore(int permitCount)
    {
        try
        {
            var result = _counter.TryAcquire(_key, permitCount, _permitLimit, _window);
            MarkDistributedHealthy();
            return ToLease(result);
        }
        catch (RateLimitStoreUnavailableException exception)
        {
            MarkDistributedUnavailable(exception);
            return _localFallback.AttemptAcquire(permitCount);
        }
    }

    protected override async ValueTask<RateLimitLease> AcquireAsyncCore(
        int permitCount,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var result = await _counter
                    .TryAcquireAsync(_key, permitCount, _permitLimit, _window, cancellationToken)
                    .ConfigureAwait(false);
                MarkDistributedHealthy();
                if (result.IsAcquired || _queueLimit == 0 || !TryReserveQueue(permitCount))
                {
                    return ToLease(result);
                }

                try
                {
                    var delay = result.RetryAfter > TimeSpan.Zero
                        ? result.RetryAfter
                        : TimeSpan.FromMilliseconds(1);
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    Interlocked.Add(ref _queuedPermits, -permitCount);
                }
            }
            catch (RateLimitStoreUnavailableException exception)
            {
                MarkDistributedUnavailable(exception);
                return await _localFallback
                    .AcquireAsync(permitCount, cancellationToken)
                    .ConfigureAwait(false);
            }
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _localFallback.Dispose();
        }

        base.Dispose(disposing);
    }

    protected override ValueTask DisposeAsyncCore()
    {
        _localFallback.Dispose();
        return ValueTask.CompletedTask;
    }

    private static RateLimitLease ToLease(RateLimitCounterResult result) =>
        new RedisRateLimitLease(result.IsAcquired, result.RetryAfter);

    private bool TryReserveQueue(int permitCount)
    {
        if (permitCount < 1 || permitCount > _queueLimit)
        {
            return false;
        }

        while (true)
        {
            var current = Volatile.Read(ref _queuedPermits);
            var next = checked(current + permitCount);
            if (next > _queueLimit)
            {
                return false;
            }

            if (Interlocked.CompareExchange(ref _queuedPermits, next, current) == current)
            {
                return true;
            }
        }
    }

    private void MarkDistributedUnavailable(Exception exception)
    {
        if (Interlocked.Exchange(ref _fallbackActive, 1) == 0)
        {
            _logger.LogError(
                exception,
                "distributed rate-limit store unavailable; using bounded local fallback");
        }
    }

    private void MarkDistributedHealthy()
    {
        if (Interlocked.Exchange(ref _fallbackActive, 0) == 1)
        {
            _logger.LogInformation("distributed rate-limit store recovered");
        }
    }
}

/// <summary>
/// Creates Redis-backed partitions while keeping client identity hashing and
/// policy names in the API security composition root.
/// </summary>
public sealed class RedisRateLimiterFactory(
    IDistributedRateLimitCounter counter,
    ApiRateLimitOptions options,
    ILoggerFactory loggerFactory)
{
    public RateLimitPartition<string> CreatePartition(
        HttpContext context,
        string policyName,
        int permitLimit,
        TimeSpan window,
        int queueLimit)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(policyName);

        if (permitLimit < 1 || window <= TimeSpan.Zero || queueLimit < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(permitLimit),
                "rate-limit settings must be positive and queue must not be negative.");
        }

        var partitionKey = $"{policyName}:{ApiRateLimitPolicies.ResolveClientKey(context)}";
        var redisKey = BuildRedisKey(options.RedisKeyPrefix, policyName, partitionKey);
        return RateLimitPartition.Get(
            partitionKey,
            _ => new RedisFixedWindowRateLimiter(
                counter,
                redisKey,
                permitLimit,
                window,
                queueLimit,
                loggerFactory.CreateLogger<RedisFixedWindowRateLimiter>()));
    }

    public static string BuildRedisKey(
        string keyPrefix,
        string policyName,
        string partitionKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(keyPrefix);
        ArgumentException.ThrowIfNullOrWhiteSpace(policyName);
        ArgumentException.ThrowIfNullOrWhiteSpace(partitionKey);

        var partitionHash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(partitionKey)))[..24];
        return $"{keyPrefix}:{policyName}:{partitionHash}";
    }
}

internal sealed class RedisRateLimitLease(
    bool isAcquired,
    TimeSpan retryAfter) : RateLimitLease
{
    public override bool IsAcquired => isAcquired;

    public override IEnumerable<string> MetadataNames =>
        isAcquired || retryAfter <= TimeSpan.Zero
            ? []
            : [MetadataName.RetryAfter.Name];

    public override bool TryGetMetadata(string metadataName, out object? metadata)
    {
        if (!isAcquired
            && retryAfter > TimeSpan.Zero
            && string.Equals(
                metadataName,
                MetadataName.RetryAfter.Name,
                StringComparison.Ordinal))
        {
            metadata = retryAfter;
            return true;
        }

        metadata = null;
        return false;
    }
}
