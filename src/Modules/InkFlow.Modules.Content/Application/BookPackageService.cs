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
/// 书籍包稳定应用入口。创建、查询、过期清理和后台生成分别委托给专用职责，
/// 保留现有 API 与 Worker 使用的单一服务契约。
/// </summary>
public sealed class BookPackageService
{
    private readonly BookPackageCreationUseCase creation;
    private readonly BookPackageQueries queries;
    private readonly BookPackageExpiration expiration;
    private readonly BookPackageGenerator generator;

    public BookPackageService(
        IBookPackageJobRepository jobs,
        ICanonicalBookRepository books,
        IContentVersionRepository versions,
        IContentPolicyReader policyReader,
        IBookPackageBuilder builder,
        IBookPackageArtifactStore artifacts,
        BookPackageOptions options,
        TimeProvider clock)
    {
        expiration = new(jobs, artifacts, clock);
        creation = new(jobs, books, policyReader, options, clock);
        queries = new(jobs, artifacts, expiration);
        generator = new(
            jobs,
            books,
            versions,
            policyReader,
            builder,
            artifacts,
            options,
            clock);
    }

    public Task<BookPackageCreateOutcome> CreateAsync(
        Guid canonicalBookId,
        BookPackageFormat format,
        CancellationToken cancellationToken = default) =>
        creation.CreateAsync(canonicalBookId, format, cancellationToken);

    public Task<BookPackageView?> GetViewAsync(
        Guid jobId,
        CancellationToken cancellationToken = default) =>
        queries.GetViewAsync(jobId, cancellationToken);

    public Task<IReadOnlyList<BookPackageView>> ListViewsAsync(
        int limit,
        CancellationToken cancellationToken = default) =>
        queries.ListViewsAsync(limit, cancellationToken);

    public Task<Stream?> OpenCompletedAsync(
        Guid jobId,
        CancellationToken cancellationToken = default) =>
        queries.OpenCompletedAsync(jobId, cancellationToken);

    public Task ProcessAsync(
        BookPackageJob job,
        CancellationToken cancellationToken = default) =>
        generator.ProcessAsync(job, cancellationToken);

    public Task ExpireOldAsync(CancellationToken cancellationToken = default) =>
        expiration.ExpireOldAsync(cancellationToken);
}
