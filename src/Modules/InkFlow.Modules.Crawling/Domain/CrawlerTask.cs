namespace InkFlow.Modules.Crawling.Domain;

/// <summary>
/// 抓取任务聚合根。全部状态流转经本类型方法完成，保证：
/// 租约互斥（同一时刻至多一个持有者）、重试计数单调、死信只发生一次。
/// </summary>
public sealed class CrawlerTask
{
    public Guid Id { get; private set; }
    public CrawlPayload Payload { get; private set; } = null!;
    public CrawlerTaskStatus Status { get; private set; } = CrawlerTaskStatus.Pending;
    public int AttemptCount { get; private set; }
    public int MaxAttempts { get; private set; }
    public string? LeaseOwner { get; private set; }
    public DateTimeOffset? LeaseExpiresAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private CrawlerTask() { }

    public static CrawlerTask Create(CrawlPayload payload, int maxAttempts = 3, DateTimeOffset? createdAt = null)
    {
        if (string.IsNullOrWhiteSpace(payload.SourceId))
        {
            throw new ArgumentException("sourceId must not be empty.", nameof(payload));
        }

        if (maxAttempts < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxAttempts), "maxAttempts must be at least 1.");
        }

        var now = createdAt ?? DateTimeOffset.UtcNow;
        return new CrawlerTask
        {
            Id = Guid.NewGuid(),
            Payload = payload,
            MaxAttempts = maxAttempts,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    /// <summary>测试/重建场景使用的完整构造。</summary>
    public static CrawlerTask Rehydrate(
        Guid id, CrawlPayload payload, CrawlerTaskStatus status, int attemptCount, int maxAttempts,
        string? leaseOwner, DateTimeOffset? leaseExpiresAt, DateTimeOffset createdAt, DateTimeOffset updatedAt) =>
        new()
        {
            Id = id,
            Payload = payload,
            Status = status,
            AttemptCount = attemptCount,
            MaxAttempts = maxAttempts,
            LeaseOwner = leaseOwner,
            LeaseExpiresAt = leaseExpiresAt,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt,
        };

    /// <summary>Pending 或租约已过期的任务可被新 worker 领取。</summary>
    public bool IsLeasable(DateTimeOffset now) =>
        Status == CrawlerTaskStatus.Pending ||
        (Status == CrawlerTaskStatus.Leased && LeaseExpiresAt is { } expiry && expiry <= now);

    public void Lease(string owner, DateTimeOffset now, TimeSpan leaseDuration)
    {
        if (string.IsNullOrWhiteSpace(owner))
        {
            throw new ArgumentException("lease owner must not be empty.", nameof(owner));
        }

        EnsureTransition(CrawlerTaskStatus.Leased);
        if (Status == CrawlerTaskStatus.Pending)
        {
            // Pending → Leased：每次领取（含过期租约被回收后的重新领取）都计入尝试，
            // 保证反复超时/崩溃的任务最终会耗尽预算进入死信，而不是无限循环。
            AttemptCount++;
        }

        Status = CrawlerTaskStatus.Leased;
        LeaseOwner = owner;
        LeaseExpiresAt = now + leaseDuration;
        Touch(now);
    }

    public void MarkRunning(DateTimeOffset now)
    {
        EnsureTransition(CrawlerTaskStatus.Running);
        Status = CrawlerTaskStatus.Running;
        Touch(now);
    }

    public void Complete(DateTimeOffset now)
    {
        EnsureTransition(CrawlerTaskStatus.Completed);
        Status = CrawlerTaskStatus.Completed;
        ClearLease();
        Touch(now);
    }

    /// <summary>
    /// 标记失败。未达重试上限时回到 Pending 等待再次领取；达到上限进入死信终态。
    /// </summary>
    public void Fail(DateTimeOffset now)
    {
        EnsureTransition(CrawlerTaskStatus.Failed);
        ClearLease();

        if (AttemptCount >= MaxAttempts)
        {
            Status = CrawlerTaskStatus.DeadLettered;
        }
        else
        {
            Status = CrawlerTaskStatus.Pending;
        }

        Touch(now);
    }

    /// <summary>把过期租约强制回收为 Pending（由维护流程调用，不消耗尝试次数）。</summary>
    public void ReleaseExpiredLease(DateTimeOffset now)
    {
        if (Status != CrawlerTaskStatus.Leased ||
            LeaseExpiresAt is not { } expiry || expiry > now)
        {
            throw new InvalidOperationException(
                "only an expired lease can be released.");
        }

        Status = CrawlerTaskStatus.Pending;
        ClearLease();
        Touch(now);
    }

    private void EnsureTransition(CrawlerTaskStatus target)
    {
        if (!CrawlerTaskTransitions.CanTransition(Status, target))
        {
            throw new InvalidOperationException(
                $"illegal crawler task transition: {Status} → {target} (task {Id}).");
        }
    }

    private void ClearLease()
    {
        LeaseOwner = null;
        LeaseExpiresAt = null;
    }

    private void Touch(DateTimeOffset now) => UpdatedAt = now;
}
