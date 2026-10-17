using InkFlow.Modules.Content.Domain;
using InkFlow.Modules.Library.Application;

namespace InkFlow.Modules.Content.Application;

public sealed record BookPackageView(
    Guid Id,
    Guid CanonicalBookId,
    BookPackageFormat Format,
    BookPackageJobStatus Status,
    int TotalChapterCount,
    int CompletedChapterCount,
    int ProgressPercent,
    string? ArtifactFileName,
    string? ArtifactSha256,
    long? ArtifactLength,
    string? FailureReason,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset ExpiresAt);

public sealed record BookPackageCreateOutcome(
    bool IsSuccess,
    BookPackageView? Package,
    string? ErrorCode,
    string? Error)
{
    public static BookPackageCreateOutcome Failure(string code, string error) =>
        new(false, null, code, error);
}

/// <summary>
/// 书籍包编排服务。生成前读取整本书的当前版本集合，形成不可变内存快照，
/// 再由 Builder 写入临时文件；只有完整文件发布后才把任务标为 Completed。
/// </summary>
public sealed class BookPackageService(
    IBookPackageJobRepository jobs,
    ICanonicalBookRepository books,
    IContentVersionRepository versions,
    IContentPolicyReader policyReader,
    IBookPackageBuilder builder,
    IBookPackageArtifactStore artifacts,
    BookPackageOptions options,
    TimeProvider clock)
{
    private readonly BookPackageGenerator generator = new(
        jobs,
        books,
        versions,
        policyReader,
        builder,
        artifacts,
        options,
        clock);

    public async Task<BookPackageCreateOutcome> CreateAsync(
        Guid canonicalBookId,
        BookPackageFormat format,
        CancellationToken cancellationToken = default)
    {
        if (canonicalBookId == Guid.Empty || !Enum.IsDefined(format))
        {
            return BookPackageCreateOutcome.Failure(
                "package.invalid-request", "book ID or package format is invalid.");
        }

        var book = await books.GetAsync(canonicalBookId, cancellationToken).ConfigureAwait(false);
        if (book is null)
        {
            return BookPackageCreateOutcome.Failure(
                "package.book-not-found", "canonical book was not found.");
        }

        if (await policyReader.IsTakedownAsync(canonicalBookId, cancellationToken).ConfigureAwait(false))
        {
            return BookPackageCreateOutcome.Failure(
                "package.book-taken-down", "a taken-down book cannot be packaged.");
        }

        if (book.Chapters.Count == 0)
        {
            return BookPackageCreateOutcome.Failure(
                "package.no-chapters", "the book has no chapters to package.");
        }

        var now = clock.GetUtcNow();
        var job = BookPackageJob.Create(
            canonicalBookId,
            format,
            now,
            now + options.Retention);
        await jobs.AddAsync(job, cancellationToken).ConfigureAwait(false);
        return new(true, ToView(job), null, null);
    }

    public async Task<BookPackageView?> GetViewAsync(
        Guid jobId,
        CancellationToken cancellationToken = default)
    {
        var job = await jobs.GetAsync(jobId, cancellationToken).ConfigureAwait(false);
        if (job is null)
        {
            return null;
        }

        await ExpireIfNeededAsync(job, cancellationToken).ConfigureAwait(false);
        return ToView(job);
    }

    public async Task<IReadOnlyList<BookPackageView>> ListViewsAsync(
        int limit,
        CancellationToken cancellationToken = default)
    {
        var values = await jobs
            .ListAsync(Math.Clamp(limit, 1, 100), cancellationToken)
            .ConfigureAwait(false);
        var views = new List<BookPackageView>(values.Count);
        foreach (var job in values)
        {
            await ExpireIfNeededAsync(job, cancellationToken).ConfigureAwait(false);
            views.Add(ToView(job));
        }

        return views;
    }

    public async Task<Stream?> OpenCompletedAsync(
        Guid jobId,
        CancellationToken cancellationToken = default)
    {
        var job = await jobs.GetAsync(jobId, cancellationToken).ConfigureAwait(false);
        if (job is null)
        {
            return null;
        }

        await ExpireIfNeededAsync(job, cancellationToken).ConfigureAwait(false);
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

    public Task ProcessAsync(
        BookPackageJob job,
        CancellationToken cancellationToken = default) =>
        generator.ProcessAsync(job, cancellationToken);

    public async Task ExpireOldAsync(
        CancellationToken cancellationToken = default)
    {
        var jobsToExpire = await jobs
            .ListExpiredAsync(clock.GetUtcNow(), 100, cancellationToken)
            .ConfigureAwait(false);
        foreach (var job in jobsToExpire)
        {
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

    private async Task ExpireIfNeededAsync(
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

    private static BookPackageView ToView(BookPackageJob job)
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
