namespace InkFlow.Modules.Content.Domain;

public enum BookPackageFormat
{
    Zip,
    Epub,
    Txt,
}

public enum BookPackageJobStatus
{
    Queued,
    Running,
    Completed,
    Failed,
    Expired,
}

/// <summary>
/// 一本正典书的一次不可变打包请求。任务状态可重试，但成功后的文件名、哈希和内容不覆盖。
/// </summary>
public sealed class BookPackageJob
{
    public Guid Id { get; private set; }
    public Guid CanonicalBookId { get; private set; }
    public BookPackageFormat Format { get; private set; }
    public BookPackageJobStatus Status { get; private set; } = BookPackageJobStatus.Queued;
    public int AttemptCount { get; private set; }
    public int MaxAttempts { get; private set; }
    public DateTimeOffset? ScheduledAt { get; private set; }
    public string? LeaseOwner { get; private set; }
    public DateTimeOffset? LeaseExpiresAt { get; private set; }
    public int TotalChapterCount { get; private set; }
    public int CompletedChapterCount { get; private set; }
    public string? ArtifactFileName { get; private set; }
    public string? ArtifactSha256 { get; private set; }
    public long? ArtifactLength { get; private set; }
    public string? FailureReason { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }

    private BookPackageJob() { }

    public static BookPackageJob Create(
        Guid canonicalBookId,
        BookPackageFormat format,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt,
        int maxAttempts = 3)
    {
        if (canonicalBookId == Guid.Empty)
        {
            throw new ArgumentException("canonicalBookId must not be empty.", nameof(canonicalBookId));
        }

        if (!Enum.IsDefined(format))
        {
            throw new ArgumentOutOfRangeException(nameof(format));
        }

        if (expiresAt <= createdAt)
        {
            throw new ArgumentException("expiresAt must be after createdAt.", nameof(expiresAt));
        }

        if (maxAttempts < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxAttempts));
        }

        return new BookPackageJob
        {
            Id = Guid.CreateVersion7(),
            CanonicalBookId = canonicalBookId,
            Format = format,
            MaxAttempts = maxAttempts,
            ScheduledAt = createdAt,
            CreatedAt = createdAt,
            UpdatedAt = createdAt,
            ExpiresAt = expiresAt,
        };
    }

    public static BookPackageJob Rehydrate(
        Guid id,
        Guid canonicalBookId,
        BookPackageFormat format,
        BookPackageJobStatus status,
        int attemptCount,
        int maxAttempts,
        DateTimeOffset? scheduledAt,
        string? leaseOwner,
        DateTimeOffset? leaseExpiresAt,
        int totalChapterCount,
        int completedChapterCount,
        string? artifactFileName,
        string? artifactSha256,
        long? artifactLength,
        string? failureReason,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        DateTimeOffset expiresAt) =>
        new()
        {
            Id = id,
            CanonicalBookId = canonicalBookId,
            Format = format,
            Status = status,
            AttemptCount = attemptCount,
            MaxAttempts = maxAttempts,
            ScheduledAt = scheduledAt,
            LeaseOwner = leaseOwner,
            LeaseExpiresAt = leaseExpiresAt,
            TotalChapterCount = totalChapterCount,
            CompletedChapterCount = completedChapterCount,
            ArtifactFileName = artifactFileName,
            ArtifactSha256 = artifactSha256,
            ArtifactLength = artifactLength,
            FailureReason = failureReason,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt,
            ExpiresAt = expiresAt,
        };

    public bool IsLeasable(DateTimeOffset now) =>
        (Status == BookPackageJobStatus.Queued &&
         (ScheduledAt is null || ScheduledAt <= now)) ||
        (Status == BookPackageJobStatus.Running &&
         LeaseExpiresAt is { } expiry && expiry <= now);

    public void Lease(string owner, DateTimeOffset now, TimeSpan leaseDuration)
    {
        if (string.IsNullOrWhiteSpace(owner))
        {
            throw new ArgumentException("lease owner must not be empty.", nameof(owner));
        }

        if (!IsLeasable(now))
        {
            throw new InvalidOperationException($"package job {Id} is not leasable.");
        }

        if (Status == BookPackageJobStatus.Queued)
        {
            AttemptCount++;
        }

        Status = BookPackageJobStatus.Running;
        ScheduledAt = null;
        LeaseOwner = owner;
        LeaseExpiresAt = now + leaseDuration;
        Touch(now);
    }

    public void SetTotalChapterCount(int count, DateTimeOffset now)
    {
        if (Status != BookPackageJobStatus.Running || count < 0)
        {
            throw new InvalidOperationException($"package job {Id} cannot set chapter count in state {Status}.");
        }

        TotalChapterCount = count;
        CompletedChapterCount = 0;
        Touch(now);
    }

    public void SetProgress(int completedChapterCount, DateTimeOffset now)
    {
        if (Status != BookPackageJobStatus.Running ||
            completedChapterCount < 0 ||
            completedChapterCount > TotalChapterCount)
        {
            throw new InvalidOperationException($"package job {Id} received invalid progress.");
        }

        CompletedChapterCount = completedChapterCount;
        Touch(now);
    }

    public void RenewLease(DateTimeOffset now, TimeSpan leaseDuration)
    {
        if (Status != BookPackageJobStatus.Running || LeaseOwner is null)
        {
            throw new InvalidOperationException($"package job {Id} has no active lease to renew.");
        }

        LeaseExpiresAt = now + leaseDuration;
        Touch(now);
    }

    public void Complete(
        string artifactFileName,
        string artifactSha256,
        long artifactLength,
        DateTimeOffset now)
    {
        if (Status != BookPackageJobStatus.Running ||
            string.IsNullOrWhiteSpace(artifactFileName) ||
            string.IsNullOrWhiteSpace(artifactSha256) ||
            artifactLength < 0 ||
            CompletedChapterCount != TotalChapterCount)
        {
            throw new InvalidOperationException($"package job {Id} cannot be completed with the supplied artifact.");
        }

        Status = BookPackageJobStatus.Completed;
        ArtifactFileName = artifactFileName.Trim();
        ArtifactSha256 = artifactSha256.Trim();
        ArtifactLength = artifactLength;
        FailureReason = null;
        ClearLease();
        Touch(now);
    }

    public void Fail(string reason, DateTimeOffset now, DateTimeOffset? nextAttemptAt = null)
    {
        if (Status != BookPackageJobStatus.Running)
        {
            throw new InvalidOperationException($"package job {Id} cannot fail from state {Status}.");
        }

        var normalized = reason?.Trim() ?? string.Empty;
        if (normalized.Length == 0)
        {
            normalized = "package generation failed.";
        }

        FailureReason = normalized.Length <= 2048 ? normalized : normalized[..2048];
        ClearLease();
        if (AttemptCount >= MaxAttempts)
        {
            Status = BookPackageJobStatus.Failed;
            ScheduledAt = null;
        }
        else
        {
            Status = BookPackageJobStatus.Queued;
            ScheduledAt = nextAttemptAt ?? now;
        }

        Touch(now);
    }

    public void Expire(DateTimeOffset now)
    {
        if (Status != BookPackageJobStatus.Completed || ExpiresAt > now)
        {
            throw new InvalidOperationException($"package job {Id} is not ready to expire.");
        }

        Status = BookPackageJobStatus.Expired;
        ClearLease();
        Touch(now);
    }

    private void ClearLease()
    {
        LeaseOwner = null;
        LeaseExpiresAt = null;
    }

    private void Touch(DateTimeOffset now) => UpdatedAt = now;
}
