namespace InkFlow.Modules.Crawling.Application;

/// <summary>
/// 重试策略：指数退避 + 上限封顶。
/// 第 n 次失败后的等待 = min(base * 2^(n-1), maxDelay)，叠加全抖动避免惊群。
/// </summary>
public sealed class RetryPolicy
{
    public TimeSpan BaseDelay { get; init; } = TimeSpan.FromSeconds(2);
    public TimeSpan MaxDelay { get; init; } = TimeSpan.FromMinutes(5);

    public TimeSpan DelayFor(int failedAttempt)
    {
        if (failedAttempt < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(failedAttempt), "attempt count starts at 1.");
        }

        var exponent = Math.Min(failedAttempt - 1, 16); // 防 int 溢出
        var backoffTicks = (long)Math.Min(
            BaseDelay.Ticks * (1L << exponent),
            MaxDelay.Ticks);

        // 全抖动（full jitter）：在 [0, backoff) 内随机取值，分散重试压力。
        var jitteredTicks = Random.Shared.NextInt64(backoffTicks + 1);
        return TimeSpan.FromTicks(jitteredTicks);
    }
}
