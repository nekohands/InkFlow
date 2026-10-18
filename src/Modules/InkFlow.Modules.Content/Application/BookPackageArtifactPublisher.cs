using System.Security.Cryptography;
using System.Text;
using InkFlow.Modules.Content.Domain;

namespace InkFlow.Modules.Content.Application;

internal sealed class BookPackageArtifactPublication(
    Guid jobId,
    int leaseAttempt,
    BookPackageFormat format,
    string temporaryPath)
{
    public Guid JobId { get; } = jobId;

    public int LeaseAttempt { get; } = leaseAttempt;

    public BookPackageFormat Format { get; } = format;

    public string TemporaryPath { get; } = temporaryPath;

    public string? ArtifactFileName { get; private set; }

    public bool IsPublished { get; private set; }

    public long Length { get; private set; }

    public string? Sha256 { get; private set; }

    internal void SetMetadata(long length, string sha256) =>
        (Length, Sha256) = (length, sha256);

    internal void MarkPublished(string artifactFileName)
    {
        ArtifactFileName = artifactFileName;
        IsPublished = true;
    }
}

/// <summary>
/// Materializes, validates, publishes and cleans up one package artifact.
/// Job leases and persistence remain with <see cref="BookPackageGenerator"/>.
/// </summary>
internal sealed class BookPackageArtifactPublisher(
    IBookPackageBuilder builder,
    IBookPackageArtifactStore artifacts,
    BookPackageOptions options)
{
    public BookPackageArtifactPublication Create(
        Guid jobId,
        int leaseAttempt,
        BookPackageFormat format) =>
        new(
            jobId,
            leaseAttempt,
            format,
            artifacts.GetTemporaryPath(jobId, leaseAttempt));

    public async Task PublishAsync(
        BookPackageArtifactPublication publication,
        BookPackageDocument document,
        Func<int, Task> progress,
        CancellationToken cancellationToken)
    {
        var estimatedBytes = EstimateUtf8Bytes(document);
        if (estimatedBytes > options.MaxPackageBytes)
        {
            throw new InvalidOperationException(
                $"book snapshot is larger than the configured package limit of {options.MaxPackageBytes} bytes.");
        }

        await using (var output = await artifacts
                         .CreateTemporaryAsync(
                             publication.JobId,
                             publication.LeaseAttempt,
                             cancellationToken)
                         .ConfigureAwait(false))
        {
            await builder
                .BuildAsync(document, publication.Format, output, progress, cancellationToken)
                .ConfigureAwait(false);
            await output.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        var length = new FileInfo(publication.TemporaryPath).Length;
        if (length > options.MaxPackageBytes)
        {
            throw new InvalidOperationException(
                $"generated package is larger than the configured package limit of {options.MaxPackageBytes} bytes.");
        }

        var digest = await ComputeSha256Async(
                publication.TemporaryPath,
                cancellationToken)
            .ConfigureAwait(false);
        publication.SetMetadata(length, digest);

        var artifactFileName = artifacts.GetArtifactFileName(
            publication.JobId,
            publication.LeaseAttempt,
            publication.Format);
        await artifacts
            .PublishAsync(publication.TemporaryPath, artifactFileName, cancellationToken)
            .ConfigureAwait(false);
        publication.MarkPublished(artifactFileName);
    }

    public async Task CleanupAsync(BookPackageArtifactPublication publication)
    {
        try
        {
            await artifacts
                .DeleteIfExistsAsync(publication.TemporaryPath)
                .ConfigureAwait(false);
            if (publication.IsPublished && publication.ArtifactFileName is not null)
            {
                await artifacts
                    .DeleteIfExistsAsync(artifacts.GetArtifactPath(publication.ArtifactFileName))
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

}
