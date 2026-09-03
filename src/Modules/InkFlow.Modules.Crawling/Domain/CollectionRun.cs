namespace InkFlow.Modules.Crawling.Domain;

/// <summary>
/// 采集运行聚合根。它是 BookInfo → Toc → Content 子任务链的唯一控制面，
/// 控制状态持久化后由任务领取查询强制执行。
/// </summary>
public sealed class CollectionRun
{
    public Guid Id { get; private set; }
    public string SourceId { get; private set; } = null!;
    public string ExternalBookId { get; private set; } = null!;
    public string InputUrl { get; private set; } = null!;
    public Guid? CanonicalBookId { get; private set; }
    public CollectionRunStatus Status { get; private set; } = CollectionRunStatus.Pending;
    public CollectionRunStage Stage { get; private set; } = CollectionRunStage.BookInfo;
    public int TotalTaskCount { get; private set; }
    public int CompletedTaskCount { get; private set; }
    public int FailedTaskCount { get; private set; }
    public string? LastError { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private CollectionRun() { }

    public static CollectionRun Create(
        string sourceId,
        string externalBookId,
        string inputUrl,
        DateTimeOffset createdAt)
    {
        if (string.IsNullOrWhiteSpace(sourceId))
        {
            throw new ArgumentException("sourceId must not be empty.", nameof(sourceId));
        }

        if (string.IsNullOrWhiteSpace(externalBookId))
        {
            throw new ArgumentException("externalBookId must not be empty.", nameof(externalBookId));
        }

        if (string.IsNullOrWhiteSpace(inputUrl))
        {
            throw new ArgumentException("inputUrl must not be empty.", nameof(inputUrl));
        }

        return new CollectionRun
        {
            Id = Guid.NewGuid(),
            SourceId = sourceId.Trim(),
            ExternalBookId = externalBookId.Trim(),
            InputUrl = inputUrl.Trim(),
            CreatedAt = createdAt,
            UpdatedAt = createdAt,
        };
    }

    public static CollectionRun Rehydrate(
        Guid id,
        string sourceId,
        string externalBookId,
        string inputUrl,
        Guid? canonicalBookId,
        CollectionRunStatus status,
        CollectionRunStage stage,
        int totalTaskCount,
        int completedTaskCount,
        int failedTaskCount,
        string? lastError,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt) =>
        new()
        {
            Id = id,
            SourceId = sourceId,
            ExternalBookId = externalBookId,
            InputUrl = inputUrl,
            CanonicalBookId = canonicalBookId,
            Status = status,
            Stage = stage,
            TotalTaskCount = totalTaskCount,
            CompletedTaskCount = completedTaskCount,
            FailedTaskCount = failedTaskCount,
            LastError = lastError,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt,
        };

    public void MarkWorkStarted(DateTimeOffset now)
    {
        if (Status == CollectionRunStatus.Pending)
        {
            Status = CollectionRunStatus.Running;
            Touch(now);
        }
    }

    public void SetCanonicalBook(Guid canonicalBookId, DateTimeOffset now)
    {
        if (canonicalBookId == Guid.Empty)
        {
            throw new ArgumentException("canonicalBookId must not be empty.", nameof(canonicalBookId));
        }

        EnsureCanScheduleFollowUp();
        CanonicalBookId = canonicalBookId;
        Touch(now);
    }

    public void AdvanceTo(CollectionRunStage stage, DateTimeOffset now)
    {
        EnsureCanScheduleFollowUp();
        if (stage < Stage)
        {
            throw new InvalidOperationException(
                $"collection run {Id} cannot move stage backwards from {Stage} to {stage}.");
        }

        if (stage != Stage)
        {
            Stage = stage;
            Touch(now);
        }
    }

    public void Pause(DateTimeOffset now)
    {
        switch (Status)
        {
            case CollectionRunStatus.Pending:
            case CollectionRunStatus.Running:
                Status = CollectionRunStatus.Paused;
                Touch(now);
                return;
            case CollectionRunStatus.Paused:
                return;
            default:
                throw new InvalidOperationException(
                    $"collection run {Id} cannot be paused from {Status}.");
        }
    }

    public void Resume(DateTimeOffset now)
    {
        if (Status == CollectionRunStatus.Paused)
        {
            // Resume deliberately returns to Pending. The next lease is the
            // durable point at which the run becomes Running again.
            Status = CollectionRunStatus.Pending;
            Touch(now);
            return;
        }

        if (Status is CollectionRunStatus.Pending or CollectionRunStatus.Running)
        {
            // Repeating the same command after it has already taken effect is a
            // successful no-op. This keeps retries from turning into a false
            // invalid-state response.
            return;
        }

        throw new InvalidOperationException(
            $"collection run {Id} can only be resumed from Paused, not {Status}.");
    }

    public void RequestStop(DateTimeOffset now)
    {
        switch (Status)
        {
            case CollectionRunStatus.Pending:
            case CollectionRunStatus.Running:
            case CollectionRunStatus.Paused:
                Status = CollectionRunStatus.Stopping;
                Touch(now);
                return;
            case CollectionRunStatus.Stopping:
            case CollectionRunStatus.Stopped:
                return;
            default:
                throw new InvalidOperationException(
                    $"collection run {Id} cannot be stopped from {Status}.");
        }
    }

    public void Cancel(DateTimeOffset now)
    {
        if (Status is CollectionRunStatus.Completed or
            CollectionRunStatus.Failed)
        {
            throw new InvalidOperationException(
                $"collection run {Id} cannot be cancelled from {Status}.");
        }

        if (Status != CollectionRunStatus.Cancelled)
        {
            Status = CollectionRunStatus.Cancelled;
            Touch(now);
        }
    }

    /// <summary>
    /// 将子任务查询结果折叠进运行聚合。子任务表仍是进度明细的事实来源，
    /// 本聚合保存最近一次快照供列表页快速展示。
    /// </summary>
    public void Reconcile(CollectionRunTaskProgress progress, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(progress);

        TotalTaskCount = progress.TotalTaskCount;
        CompletedTaskCount = progress.CompletedTaskCount;
        FailedTaskCount = progress.DeadLetteredTaskCount;

        if (Status is CollectionRunStatus.Completed or
            CollectionRunStatus.Failed or
            CollectionRunStatus.Stopped or
            CollectionRunStatus.Cancelled)
        {
            Touch(now);
            return;
        }

        if (Status == CollectionRunStatus.Stopping)
        {
            if (progress.InFlightTaskCount == 0)
            {
                Status = CollectionRunStatus.Stopped;
                LastError = null;
            }

            Touch(now);
            return;
        }

        if (progress.DeadLetteredTaskCount > 0)
        {
            Status = CollectionRunStatus.Failed;
            LastError = "one or more required collection tasks reached the dead-letter state.";
            Touch(now);
            return;
        }

        if (Status != CollectionRunStatus.Paused &&
            Stage == CollectionRunStage.Content &&
            progress.TotalTaskCount > 0 &&
            progress.CompletedTaskCount == progress.TotalTaskCount)
        {
            Status = CollectionRunStatus.Completed;
            LastError = null;
        }

        Touch(now);
    }

    public bool CanScheduleFollowUp => Status is
        CollectionRunStatus.Pending or
        CollectionRunStatus.Running or
        CollectionRunStatus.Paused;

    private void EnsureCanScheduleFollowUp()
    {
        if (!CanScheduleFollowUp)
        {
            throw new InvalidOperationException(
                $"collection run {Id} does not accept follow-up work in state {Status}.");
        }
    }

    private void Touch(DateTimeOffset now) => UpdatedAt = now;
}

/// <summary>采集运行子任务的实时聚合进度。</summary>
public sealed record CollectionRunTaskProgress(
    int TotalTaskCount,
    int PendingTaskCount,
    int LeasedTaskCount,
    int RunningTaskCount,
    int CompletedTaskCount,
    int DeadLetteredTaskCount,
    int CancelledTaskCount)
{
    public int InFlightTaskCount => LeasedTaskCount + RunningTaskCount;
    public int RemainingTaskCount => Math.Max(0, TotalTaskCount - CompletedTaskCount - DeadLetteredTaskCount - CancelledTaskCount);
}
