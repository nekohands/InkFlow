using InkFlow.Modules.Content.Domain;

namespace InkFlow.Modules.Content.Application;

internal static class BookPackageViewMapper
{
    public static BookPackageView ToView(BookPackageJob job)
    {
        var completed = Math.Clamp(job.CompletedChapterCount, 0, job.TotalChapterCount);
        var percent = job.TotalChapterCount == 0
            ? 0
            : Math.Clamp((int)Math.Round(
                (double)completed / job.TotalChapterCount * 100,
                MidpointRounding.AwayFromZero), 0, 100);
        return new(
            job.Id,
            job.CanonicalBookId,
            job.Format,
            job.Status,
            job.TotalChapterCount,
            completed,
            percent,
            job.ArtifactFileName,
            job.ArtifactSha256,
            job.ArtifactLength,
            job.FailureReason,
            job.CreatedAt,
            job.UpdatedAt,
            job.ExpiresAt);
    }
}
