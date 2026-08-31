using InkFlow.Modules.Content.Domain;

namespace InkFlow.Modules.Content.Application;

public sealed record BookPackageChapter(
    Guid Id,
    int Index,
    string Title,
    string CanonicalText,
    Guid ContentVersionId,
    string CanonicalHash);

/// <summary>打包开始时固定的书籍与当前正文版本快照。</summary>
public sealed record BookPackageDocument(
    Guid BookId,
    string Title,
    string Author,
    IReadOnlyList<BookPackageChapter> Chapters,
    DateTimeOffset? GeneratedAt = null);

public interface IBookPackageJobRepository
{
    Task AddAsync(BookPackageJob job, CancellationToken cancellationToken = default);

    Task<BookPackageJob?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<BookPackageJob?> TryLeaseAsync(
        DateTimeOffset now,
        string owner,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default);

    Task SaveAsync(BookPackageJob job, CancellationToken cancellationToken = default);

    /// <summary>
    /// 仅在调用方仍持有指定租约尝试时保存任务变更；返回 false 表示租约已被回收或替换。
    /// </summary>
    Task<bool> SaveLeasedAsync(
        BookPackageJob job,
        string leaseOwner,
        int leaseAttempt,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BookPackageJob>> ListExpiredAsync(
        DateTimeOffset now,
        int limit,
        CancellationToken cancellationToken = default);
}

public interface IBookPackageBuilder
{
    Task BuildAsync(
        BookPackageDocument document,
        BookPackageFormat format,
        Stream output,
        Func<int, Task> progress,
        CancellationToken cancellationToken = default);
}

public interface IBookPackageArtifactStore
{
    string GetTemporaryPath(Guid jobId);

    /// <summary>按租约尝试隔离临时文件，避免旧 Worker 清理新 Worker 的中间产物。</summary>
    string GetTemporaryPath(Guid jobId, int leaseAttempt);

    Task<Stream> CreateTemporaryAsync(Guid jobId, CancellationToken cancellationToken = default);

    Task<Stream> CreateTemporaryAsync(
        Guid jobId,
        int leaseAttempt,
        CancellationToken cancellationToken = default);

    string GetArtifactPath(string artifactFileName);

    string GetArtifactFileName(Guid jobId, BookPackageFormat format);

    /// <summary>按租约尝试生成最终文件名，避免过期 Worker 与新 Worker 争用同一文件。</summary>
    string GetArtifactFileName(Guid jobId, int leaseAttempt, BookPackageFormat format);

    Task PublishAsync(string temporaryPath, string artifactFileName, CancellationToken cancellationToken = default);

    Task DeleteIfExistsAsync(string filePath, CancellationToken cancellationToken = default);

    Task<Stream> OpenReadAsync(string artifactFileName, CancellationToken cancellationToken = default);
}

public sealed record BookPackageOptions(
    string RootDirectory,
    int MaxChapters,
    long MaxPackageBytes,
    TimeSpan Retention,
    TimeSpan LeaseDuration)
{
    public static BookPackageOptions Default { get; } = new(
        "/var/lib/inkflow/packages",
        10_000,
        256L * 1024 * 1024,
        TimeSpan.FromDays(7),
        TimeSpan.FromMinutes(10));

    public static BookPackageOptions FromEnvironment()
    {
        var defaults = Default;
        var root = Environment.GetEnvironmentVariable("INKFLOW_PACKAGES_ROOT");
        var retentionDays = ParsePositiveDouble(
            Environment.GetEnvironmentVariable("INKFLOW_PACKAGES_RETENTION_DAYS"),
            defaults.Retention.TotalDays);
        var maxChapters = ParsePositiveInt(
            Environment.GetEnvironmentVariable("INKFLOW_PACKAGES_MAX_CHAPTERS"),
            defaults.MaxChapters);
        var maxBytes = ParsePositiveLong(
            Environment.GetEnvironmentVariable("INKFLOW_PACKAGES_MAX_BYTES"),
            defaults.MaxPackageBytes);

        return defaults with
        {
            RootDirectory = string.IsNullOrWhiteSpace(root) ? defaults.RootDirectory : root.Trim(),
            Retention = TimeSpan.FromDays(retentionDays),
            MaxChapters = maxChapters,
            MaxPackageBytes = maxBytes,
        };
    }

    private static int ParsePositiveInt(string? value, int fallback) =>
        int.TryParse(value, out var parsed) && parsed > 0 ? parsed : fallback;

    private static long ParsePositiveLong(string? value, long fallback) =>
        long.TryParse(value, out var parsed) && parsed > 0 ? parsed : fallback;

    private static double ParsePositiveDouble(string? value, double fallback) =>
        double.TryParse(value, out var parsed) && parsed > 0 ? parsed : fallback;
}
