using System.Security.Cryptography;
using System.Text;
using InkFlow.Modules.Content.Domain;
using InkFlow.Modules.Library.Application;

namespace InkFlow.Modules.Content.Application;

/// <summary>
/// Generates and publishes one leased package job. The public package facade
/// keeps job creation, queries and expiry separate from this worker lifecycle.
/// </summary>
internal sealed class BookPackageGenerator(
    IBookPackageJobRepository jobs,
    ICanonicalBookRepository books,
    IContentVersionRepository versions,
    IContentPolicyReader policyReader,
    IBookPackageBuilder builder,
    IBookPackageArtifactStore artifacts,
    BookPackageOptions options,
    TimeProvider clock)
{
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
            var document = await BookPackageSnapshotFactory.CreateAsync(
                    books,
                    versions,
                    policyReader,
                    job.CanonicalBookId,
                    snapshotAt,
                    cancellationToken)
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

    private sealed class PackageLeaseLostException(Guid jobId, int leaseAttempt)
        : InvalidOperationException(
            $"package job {jobId} lease attempt {leaseAttempt} is no longer active.");
}
