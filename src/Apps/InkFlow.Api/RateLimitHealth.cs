namespace InkFlow.Api;

public enum RateLimitStoreHealthStatus
{
    Unknown,
    Healthy,
    Unavailable,
}

/// <summary>
/// 当前 API 限流计数存储的最小健康快照。它只描述可用性和时间/次数，
/// 不携带连接串、异常文本或任何客户端身份。
/// </summary>
public sealed record RateLimitStoreHealthSnapshot(
    RateLimitStoreHealthStatus Status,
    int ConsecutiveFailures,
    DateTimeOffset? LastSuccessAt,
    DateTimeOffset? LastFailureAt);

public interface IRateLimitStoreHealthReader
{
    RateLimitStoreHealthSnapshot GetSnapshot();
}

public interface IRateLimitStoreHealthRecorder
{
    void RecordSuccess();

    void RecordFailure();
}

/// <summary>
/// Redis 限流计数器共享的进程内健康状态。它是可重建的观测状态，
/// 不参与限流判定，也不替代 Redis/数据库事实。
/// </summary>
public sealed class RateLimitStoreHealth(TimeProvider clock) :
    IRateLimitStoreHealthReader,
    IRateLimitStoreHealthRecorder
{
    private readonly object _gate = new();
    private readonly TimeProvider _clock =
        clock ?? throw new ArgumentNullException(nameof(clock));
    private RateLimitStoreHealthStatus _status = RateLimitStoreHealthStatus.Unknown;
    private int _consecutiveFailures;
    private DateTimeOffset? _lastSuccessAt;
    private DateTimeOffset? _lastFailureAt;

    public RateLimitStoreHealthSnapshot GetSnapshot()
    {
        lock (_gate)
        {
            return new RateLimitStoreHealthSnapshot(
                _status,
                _consecutiveFailures,
                _lastSuccessAt,
                _lastFailureAt);
        }
    }

    public void RecordSuccess()
    {
        lock (_gate)
        {
            _status = RateLimitStoreHealthStatus.Healthy;
            _consecutiveFailures = 0;
            _lastSuccessAt = _clock.GetUtcNow();
        }
    }

    public void RecordFailure()
    {
        lock (_gate)
        {
            _status = RateLimitStoreHealthStatus.Unavailable;
            _consecutiveFailures =
                _consecutiveFailures == int.MaxValue
                    ? int.MaxValue
                    : _consecutiveFailures + 1;
            _lastFailureAt = _clock.GetUtcNow();
        }
    }
}
