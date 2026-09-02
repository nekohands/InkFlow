using System.Security.Cryptography;
using System.Text;
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

    public async Task ProcessAsync(
        BookPackageJob job,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(job);
        var leaseOwner = job.LeaseOwner;
        var leaseAttempt = job.AttemptCount;
        if (string.IsNullOrWhiteSpace(leaseOwner) || leaseAttempt < 1)
        {
            throw new InvalidOperationException(
                $"package job {job.Id} must have an active lease before processing.");
        }

        var temporaryPath = artifacts.GetTemporaryPath(job.Id, leaseAttempt);
        string? artifactFileName = null;
        var published = false;

        try
        {
            var snapshotAt = clock.GetUtcNow();
            var document = await CreateSnapshotAsync(job.CanonicalBookId, snapshotAt, cancellationToken)
                .ConfigureAwait(false);
            if (document.Chapters.Count > options.MaxChapters)
            {
                throw new InvalidOperationException(
                    $"book contains {document.Chapters.Count} chapters; package limit is {options.MaxChapters}.");
            }

            var estimatedBytes = EstimateUtf8Bytes(document);
            if (estimatedBytes > options.MaxPackageBytes)
            {
                throw new InvalidOperationException(
                    $"book snapshot is larger than the configured package limit of {options.MaxPackageBytes} bytes.");
            }

            job.SetTotalChapterCount(document.Chapters.Count, clock.GetUtcNow());
            await SaveLeasedOrThrowAsync(
                    job,
                    leaseOwner,
                    leaseAttempt,
                    cancellationToken)
                .ConfigureAwait(false);

            await using (var output = await artifacts
                             .CreateTemporaryAsync(job.Id, leaseAttempt, cancellationToken)
                             .ConfigureAwait(false))
            {
                async Task OnProgress(int completed)
                {
                    job.SetProgress(completed, clock.GetUtcNow());
                    job.RenewLease(clock.GetUtcNow(), options.LeaseDuration);
                    await SaveLeasedOrThrowAsync(
                            job,
                            leaseOwner,
                            leaseAttempt,
                            cancellationToken)
                        .ConfigureAwait(false);
                }

                await builder
                    .BuildAsync(document, job.Format, output, OnProgress, cancellationToken)
                    .ConfigureAwait(false);
                await output.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            var length = new FileInfo(temporaryPath).Length;
            if (length > options.MaxPackageBytes)
            {
                throw new InvalidOperationException(
                    $"generated package is larger than the configured package limit of {options.MaxPackageBytes} bytes.");
            }

            var digest = await ComputeSha256Async(temporaryPath, cancellationToken).ConfigureAwait(false);
            artifactFileName = artifacts.GetArtifactFileName(job.Id, leaseAttempt, job.Format);
            await artifacts
                .PublishAsync(temporaryPath, artifactFileName, cancellationToken)
                .ConfigureAwait(false);
            published = true;

            if (await policyReader.IsTakedownAsync(job.CanonicalBookId, cancellationToken).ConfigureAwait(false))
            {
                throw new InvalidOperationException("book was taken down while the package was being generated.");
            }

            job.Complete(artifactFileName, digest, length, clock.GetUtcNow());
            await SaveLeasedOrThrowAsync(
                    job,
                    leaseOwner,
                    leaseAttempt,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (PackageLeaseLostException)
        {
            await DeleteArtifactsAsync(temporaryPath, artifactFileName, published).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await DeleteArtifactsAsync(temporaryPath, artifactFileName, published).ConfigureAwait(false);
            throw;
        }
        catch (Exception)
        {
            await DeleteArtifactsAsync(temporaryPath, artifactFileName, published).ConfigureAwait(false);
            job.Fail(
                "package generation failed.",
                clock.GetUtcNow(),
                clock.GetUtcNow() + TimeSpan.FromSeconds(15));
            _ = await jobs
                .SaveLeasedAsync(
                    job,
                    leaseOwner,
                    leaseAttempt,
                    clock.GetUtcNow(),
                    cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private async Task SaveLeasedOrThrowAsync(
        BookPackageJob job,
        string leaseOwner,
        int leaseAttempt,
        CancellationToken cancellationToken)
    {
        var saved = await jobs
            .SaveLeasedAsync(
                job,
                leaseOwner,
                leaseAttempt,
                clock.GetUtcNow(),
                cancellationToken)
            .ConfigureAwait(false);
        if (!saved)
        {
            throw new PackageLeaseLostException(job.Id, leaseAttempt);
        }
    }

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

    private async Task<BookPackageDocument> CreateSnapshotAsync(
        Guid canonicalBookId,
        DateTimeOffset snapshotAt,
        CancellationToken cancellationToken)
    {
        if (await policyReader.IsTakedownAsync(canonicalBookId, cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidOperationException("taken-down books cannot be packaged.");
        }

        var book = await books.GetAsync(canonicalBookId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("canonical book was not found.");
        var currentVersions = await versions
            .ListCurrentForBookAsync(canonicalBookId, cancellationToken)
            .ConfigureAwait(false);
        var currentByChapter = currentVersions.ToDictionary(version => version.CanonicalChapterId);
        var snapshot = new List<BookPackageChapter>(book.Chapters.Count);
        foreach (var chapter in book.Chapters.OrderBy(chapter => chapter.Index))
        {
            if (!currentByChapter.TryGetValue(chapter.Id, out var version) ||
                version.CanonicalBookId != book.Id ||
                version.CanonicalChapterId != chapter.Id ||
                string.IsNullOrWhiteSpace(version.CanonicalText))
            {
                throw new InvalidOperationException(
                    $"chapter {chapter.Id} does not have a current published content version.");
            }

            snapshot.Add(new BookPackageChapter(
                chapter.Id,
                chapter.Index,
                chapter.Title,
                version.CanonicalText,
                version.Id,
                version.CanonicalHash));
        }

        return new(book.Id, book.Title, book.Author, snapshot, snapshotAt);
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

    private async Task DeleteArtifactsAsync(
        string temporaryPath,
        string? artifactFileName,
        bool published)
    {
        try
        {
            await artifacts.DeleteIfExistsAsync(temporaryPath).ConfigureAwait(false);
            if (published && artifactFileName is not null)
            {
                await artifacts
                    .DeleteIfExistsAsync(artifacts.GetArtifactPath(artifactFileName))
                    .ConfigureAwait(false);
            }
        }
        catch
        {
            // Do not mask the original generation/cancellation result. The next
            // retention or operator cleanup pass can remove a stranded temp file.
        }
    }

    private static long EstimateUtf8Bytes(BookPackageDocument document)
    {
        long total = Encoding.UTF8.GetByteCount(document.Title) +
                     Encoding.UTF8.GetByteCount(document.Author);
        foreach (var chapter in document.Chapters)
        {
            total = checked(total +
                Encoding.UTF8.GetByteCount(chapter.Title) +
                Encoding.UTF8.GetByteCount(chapter.CanonicalText));
        }

        return total;
    }

    private static async Task<string> ComputeSha256Async(
        string path,
        CancellationToken cancellationToken)
    {
        await using var input = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 64 * 1024,
            options: FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var sha256 = SHA256.Create();
        var hash = await sha256.ComputeHashAsync(input, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexString(hash).ToLowerInvariant();
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

    private sealed class PackageLeaseLostException(Guid jobId, int leaseAttempt)
        : InvalidOperationException(
            $"package job {jobId} lease attempt {leaseAttempt} is no longer active.");
}
