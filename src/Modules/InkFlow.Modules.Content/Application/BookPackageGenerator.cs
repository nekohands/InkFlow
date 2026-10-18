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
    private readonly BookPackageArtifactPublisher artifactPublisher =
        new(builder, artifacts, options);

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

        var publication = artifactPublisher.Create(job.Id, leaseAttempt, job.Format);

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

            job.SetTotalChapterCount(document.Chapters.Count, clock.GetUtcNow());
            await SaveLeasedOrThrowAsync(
                    job,
                    leaseOwner,
                    leaseAttempt,
                    cancellationToken)
                .ConfigureAwait(false);

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

            await artifactPublisher
                .PublishAsync(
                    publication,
                    document,
                    OnProgress,
                    cancellationToken)
                .ConfigureAwait(false);

            if (await policyReader.IsTakedownAsync(job.CanonicalBookId, cancellationToken).ConfigureAwait(false))
            {
                throw new InvalidOperationException("book was taken down while the package was being generated.");
            }

            job.Complete(
                publication.ArtifactFileName!,
                publication.Sha256!,
                publication.Length,
                clock.GetUtcNow());
            await SaveLeasedOrThrowAsync(
                    job,
                    leaseOwner,
                    leaseAttempt,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (PackageLeaseLostException)
        {
            await artifactPublisher.CleanupAsync(publication).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await artifactPublisher.CleanupAsync(publication).ConfigureAwait(false);
            throw;
        }
        catch (Exception)
        {
            await artifactPublisher.CleanupAsync(publication).ConfigureAwait(false);
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

    private sealed class PackageLeaseLostException(Guid jobId, int leaseAttempt)
        : InvalidOperationException(
            $"package job {jobId} lease attempt {leaseAttempt} is no longer active.");
}
