using InkFlow.Modules.Content.Domain;

namespace InkFlow.Modules.Content.Infrastructure.Persistence;

public static class BookPackageJobMapper
{
    public static BookPackageJobEntity ToEntity(BookPackageJob job) =>
        new()
        {
            Id = job.Id,
            CanonicalBookId = job.CanonicalBookId,
            Format = (int)job.Format,
            Status = (int)job.Status,
            AttemptCount = job.AttemptCount,
            MaxAttempts = job.MaxAttempts,
            ScheduledAt = job.ScheduledAt,
            LeaseOwner = job.LeaseOwner,
            LeaseExpiresAt = job.LeaseExpiresAt,
            TotalChapterCount = job.TotalChapterCount,
            CompletedChapterCount = job.CompletedChapterCount,
            ArtifactFileName = job.ArtifactFileName,
            ArtifactSha256 = job.ArtifactSha256,
            ArtifactLength = job.ArtifactLength,
            FailureReason = job.FailureReason,
            CreatedAt = job.CreatedAt,
            UpdatedAt = job.UpdatedAt,
            ExpiresAt = job.ExpiresAt,
        };

    public static BookPackageJob ToDomain(BookPackageJobEntity entity) =>
        BookPackageJob.Rehydrate(
            entity.Id,
            entity.CanonicalBookId,
            (BookPackageFormat)entity.Format,
            (BookPackageJobStatus)entity.Status,
            entity.AttemptCount,
            entity.MaxAttempts,
            entity.ScheduledAt,
            entity.LeaseOwner,
            entity.LeaseExpiresAt,
            entity.TotalChapterCount,
            entity.CompletedChapterCount,
            entity.ArtifactFileName,
            entity.ArtifactSha256,
            entity.ArtifactLength,
            entity.FailureReason,
            entity.CreatedAt,
            entity.UpdatedAt,
            entity.ExpiresAt);

    public static void ApplyDomain(BookPackageJob job, BookPackageJobEntity entity)
    {
        var fresh = ToEntity(job);
        entity.Status = fresh.Status;
        entity.AttemptCount = fresh.AttemptCount;
        entity.MaxAttempts = fresh.MaxAttempts;
        entity.ScheduledAt = fresh.ScheduledAt;
        entity.LeaseOwner = fresh.LeaseOwner;
        entity.LeaseExpiresAt = fresh.LeaseExpiresAt;
        entity.TotalChapterCount = fresh.TotalChapterCount;
        entity.CompletedChapterCount = fresh.CompletedChapterCount;
        entity.ArtifactFileName = fresh.ArtifactFileName;
        entity.ArtifactSha256 = fresh.ArtifactSha256;
        entity.ArtifactLength = fresh.ArtifactLength;
        entity.FailureReason = fresh.FailureReason;
        entity.UpdatedAt = fresh.UpdatedAt;
        entity.ExpiresAt = fresh.ExpiresAt;
    }
}
