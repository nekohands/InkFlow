using InkFlow.Modules.Content.Domain;

namespace InkFlow.Modules.Content.Application;

/// <summary>统一处理书籍包产物过期与任务状态清理。</summary>
internal sealed class BookPackageExpiration(
    IBookPackageJobRepository jobs,
    IBookPackageArtifactStore artifacts,
    TimeProvider clock)
{
    public async Task ExpireOldAsync(CancellationToken cancellationToken)
    {
        var jobsToExpire = await jobs
            .ListExpiredAsync(clock.GetUtcNow(), 100, cancellationToken)
            .ConfigureAwait(false);
        foreach (var job in jobsToExpire)
        {
            await ExpireIfNeededAsync(job, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task ExpireIfNeededAsync(
        BookPackageJob job,
        CancellationToken cancellationToken)
    {
        if (job.Status != BookPackageJobStatus.Completed || job.ExpiresAt > clock.GetUtcNow())
        {
            return;
        }

        if (job.ArtifactFileName is { } fileName)
        {
            await artifacts
                .DeleteIfExistsAsync(artifacts.GetArtifactPath(fileName), cancellationToken)
                .ConfigureAwait(false);
        }

        job.Expire(clock.GetUtcNow());
        await jobs.SaveAsync(job, cancellationToken).ConfigureAwait(false);
    }
}
