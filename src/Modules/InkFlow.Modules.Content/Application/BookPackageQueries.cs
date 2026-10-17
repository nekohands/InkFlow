using InkFlow.Modules.Content.Domain;

namespace InkFlow.Modules.Content.Application;

/// <summary>书籍包状态与已完成产物的用户侧读取。</summary>
internal sealed class BookPackageQueries(
    IBookPackageJobRepository jobs,
    IBookPackageArtifactStore artifacts,
    BookPackageExpiration expiration)
{
    public async Task<BookPackageView?> GetViewAsync(
        Guid jobId,
        CancellationToken cancellationToken)
    {
        var job = await jobs.GetAsync(jobId, cancellationToken).ConfigureAwait(false);
        if (job is null)
        {
            return null;
        }

        await expiration.ExpireIfNeededAsync(job, cancellationToken).ConfigureAwait(false);
        return BookPackageViewMapper.ToView(job);
    }

    public async Task<IReadOnlyList<BookPackageView>> ListViewsAsync(
        int limit,
        CancellationToken cancellationToken)
    {
        var values = await jobs
            .ListAsync(Math.Clamp(limit, 1, 100), cancellationToken)
            .ConfigureAwait(false);
        var views = new List<BookPackageView>(values.Count);
        foreach (var job in values)
        {
            await expiration.ExpireIfNeededAsync(job, cancellationToken).ConfigureAwait(false);
            views.Add(BookPackageViewMapper.ToView(job));
        }

        return views;
    }

    public async Task<Stream?> OpenCompletedAsync(
        Guid jobId,
        CancellationToken cancellationToken)
    {
        var job = await jobs.GetAsync(jobId, cancellationToken).ConfigureAwait(false);
        if (job is null)
        {
            return null;
        }

        await expiration.ExpireIfNeededAsync(job, cancellationToken).ConfigureAwait(false);
        if (job.Status != BookPackageJobStatus.Completed ||
            string.IsNullOrWhiteSpace(job.ArtifactFileName))
        {
            return null;
        }

        try
        {
            return await artifacts
                .OpenReadAsync(job.ArtifactFileName, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception)
            when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return null;
        }
    }
}
