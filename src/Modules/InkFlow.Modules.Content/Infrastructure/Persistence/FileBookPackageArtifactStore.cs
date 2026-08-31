using InkFlow.Modules.Content.Application;
using InkFlow.Modules.Content.Domain;

namespace InkFlow.Modules.Content.Infrastructure.Persistence;

/// <summary>
/// 书籍包文件存储。临时文件与最终文件位于同一受限目录，完成时原子改名，
/// API 只接受由任务 ID 生成的文件名。
/// </summary>
public sealed class FileBookPackageArtifactStore(BookPackageOptions options) : IBookPackageArtifactStore
{
    private readonly string _rootDirectory = Path.GetFullPath(options.RootDirectory);

    public string GetTemporaryPath(Guid jobId)
    {
        if (jobId == Guid.Empty)
        {
            throw new ArgumentException("jobId must not be empty.", nameof(jobId));
        }

        return Path.Combine(_rootDirectory, $".{jobId:N}.tmp");
    }

    public string GetTemporaryPath(Guid jobId, int leaseAttempt)
    {
        ValidateLeaseAttempt(leaseAttempt);
        if (jobId == Guid.Empty)
        {
            throw new ArgumentException("jobId must not be empty.", nameof(jobId));
        }

        return Path.Combine(_rootDirectory, $".{jobId:N}.{leaseAttempt}.tmp");
    }

    public Task<Stream> CreateTemporaryAsync(
        Guid jobId,
        CancellationToken cancellationToken = default)
        => CreateTemporaryCoreAsync(GetTemporaryPath(jobId), cancellationToken);

    public Task<Stream> CreateTemporaryAsync(
        Guid jobId,
        int leaseAttempt,
        CancellationToken cancellationToken = default)
        => CreateTemporaryCoreAsync(GetTemporaryPath(jobId, leaseAttempt), cancellationToken);

    private Task<Stream> CreateTemporaryCoreAsync(
        string path,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Directory.CreateDirectory(_rootDirectory);
        Stream stream = new FileStream(
            path,
            FileMode.Create,
            FileAccess.ReadWrite,
            FileShare.None,
            bufferSize: 64 * 1024,
            options: FileOptions.Asynchronous | FileOptions.SequentialScan);
        return Task.FromResult(stream);
    }

    public string GetArtifactPath(string artifactFileName)
    {
        ValidateArtifactFileName(artifactFileName);
        return Path.Combine(_rootDirectory, artifactFileName);
    }

    public string GetArtifactFileName(Guid jobId, BookPackageFormat format)
    {
        if (jobId == Guid.Empty)
        {
            throw new ArgumentException("jobId must not be empty.", nameof(jobId));
        }

        var extension = format switch
        {
            BookPackageFormat.Zip => "zip",
            BookPackageFormat.Epub => "epub",
            BookPackageFormat.Txt => "txt",
            _ => throw new ArgumentOutOfRangeException(nameof(format)),
        };
        return $"{jobId:N}.{extension}";
    }

    public string GetArtifactFileName(
        Guid jobId,
        int leaseAttempt,
        BookPackageFormat format)
    {
        ValidateLeaseAttempt(leaseAttempt);
        if (jobId == Guid.Empty)
        {
            throw new ArgumentException("jobId must not be empty.", nameof(jobId));
        }

        var extension = format switch
        {
            BookPackageFormat.Zip => "zip",
            BookPackageFormat.Epub => "epub",
            BookPackageFormat.Txt => "txt",
            _ => throw new ArgumentOutOfRangeException(nameof(format)),
        };
        return $"{jobId:N}-{leaseAttempt}.{extension}";
    }

    public Task PublishAsync(
        string temporaryPath,
        string artifactFileName,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var expectedTemporaryPath = GetTemporaryPathFromPath(temporaryPath);
        var artifactPath = GetArtifactPath(artifactFileName);
        Directory.CreateDirectory(_rootDirectory);
        if (!File.Exists(expectedTemporaryPath))
        {
            throw new FileNotFoundException("package temporary artifact was not found.");
        }

        if (File.Exists(artifactPath))
        {
            throw new IOException("package artifact already exists.");
        }

        File.Move(expectedTemporaryPath, artifactPath);
        return Task.CompletedTask;
    }

    public Task DeleteIfExistsAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var fullPath = Path.GetFullPath(filePath);
        if (!IsWithinRoot(fullPath))
        {
            throw new InvalidOperationException("package artifact path is outside the configured root.");
        }

        File.Delete(fullPath);
        return Task.CompletedTask;
    }

    public Task<Stream> OpenReadAsync(
        string artifactFileName,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = GetArtifactPath(artifactFileName);
        Stream stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 64 * 1024,
            options: FileOptions.Asynchronous | FileOptions.SequentialScan);
        return Task.FromResult(stream);
    }

    private string GetTemporaryPathFromPath(string temporaryPath)
    {
        var fullPath = Path.GetFullPath(temporaryPath);
        if (!IsWithinRoot(fullPath) ||
            !Path.GetFileName(fullPath).StartsWith(".", StringComparison.Ordinal) ||
            !fullPath.EndsWith(".tmp", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("package temporary artifact path is invalid.");
        }

        return fullPath;
    }

    private bool IsWithinRoot(string fullPath)
    {
        var root = _rootDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                   Path.DirectorySeparatorChar;
        return fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase);
    }

    private static void ValidateArtifactFileName(string artifactFileName)
    {
        if (string.IsNullOrWhiteSpace(artifactFileName) ||
            Path.GetFileName(artifactFileName) != artifactFileName ||
            artifactFileName.Length > 256)
        {
            throw new ArgumentException("artifact file name is invalid.", nameof(artifactFileName));
        }

        var extension = Path.GetExtension(artifactFileName);
        if (extension is not ".zip" and not ".epub" and not ".txt")
        {
            throw new ArgumentException("artifact file extension is invalid.", nameof(artifactFileName));
        }
    }

    private static void ValidateLeaseAttempt(int leaseAttempt)
    {
        if (leaseAttempt < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(leaseAttempt));
        }
    }
}
