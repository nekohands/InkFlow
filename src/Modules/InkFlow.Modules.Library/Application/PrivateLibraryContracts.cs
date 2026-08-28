using InkFlow.Modules.Library.Domain;

namespace InkFlow.Modules.Library.Application;

public sealed record PrivateBookView(
    Guid PrivateBookId,
    string Title,
    string? Author,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public enum PrivateLibraryResultStatus
{
    Success = 1,
    NotFound = 2,
    InvalidRequest = 3,
    UnsupportedFormat = 4,
    FileTooLarge = 5,
    InvalidFile = 6,
}

public sealed record PrivateLibraryOperationResult<T>(
    PrivateLibraryResultStatus Status,
    T? Value);

/// <summary>私有书目仓储：每个读取和变更都显式按 UserId 限定。</summary>
public interface IPrivateBookRepository
{
    Task AddAsync(PrivateBook book, CancellationToken cancellationToken = default);

    Task<PrivateBook?> GetAsync(
        Guid userId,
        Guid privateBookId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PrivateBook>> ListAsync(
        Guid userId,
        int limit,
        CancellationToken cancellationToken = default);

    Task<bool> SaveAsync(PrivateBook book, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(
        Guid userId,
        Guid privateBookId,
        CancellationToken cancellationToken = default);

    Task AddWithChaptersAsync(
        PrivateBook book,
        IReadOnlyCollection<PrivateChapter> chapters,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PrivateChapter>> ListChaptersAsync(
        Guid userId,
        Guid privateBookId,
        CancellationToken cancellationToken = default);

    Task<PrivateChapter?> GetChapterAsync(
        Guid userId,
        Guid privateBookId,
        Guid privateChapterId,
        CancellationToken cancellationToken = default);
}

public interface IPrivateLibraryService
{
    Task<PrivateLibraryOperationResult<PrivateBookView>> CreateAsync(
        Guid userId,
        string? title,
        string? author,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PrivateBookView>> ListAsync(
        Guid userId,
        int limit,
        CancellationToken cancellationToken = default);

    Task<PrivateBookView?> GetAsync(
        Guid userId,
        Guid privateBookId,
        CancellationToken cancellationToken = default);

    Task<PrivateLibraryOperationResult<PrivateBookView>> UpdateAsync(
        Guid userId,
        Guid privateBookId,
        string? title,
        string? author,
        CancellationToken cancellationToken = default);

    Task<PrivateLibraryResultStatus> DeleteAsync(
        Guid userId,
        Guid privateBookId,
        CancellationToken cancellationToken = default);
}

public sealed record PrivateChapterView(
    Guid PrivateChapterId,
    int Index,
    string Title,
    int ParagraphCount,
    DateTimeOffset CreatedAt);

public sealed record PrivateChapterContentView(
    Guid PrivateChapterId,
    Guid PrivateBookId,
    int Index,
    string Title,
    string ContentHash,
    IReadOnlyList<string> Paragraphs);

public sealed record PrivateBookImportView(
    PrivateBookView Book,
    int ChapterCount);

public sealed record PrivateLibraryExport(
    string FileName,
    string ContentType,
    byte[] Content);

public sealed record PrivateBookImportDraft(
    string Title,
    string? Author,
    IReadOnlyList<PrivateChapterImportDraft> Chapters);

public sealed record PrivateChapterImportDraft(
    string Title,
    IReadOnlyList<string> Paragraphs);

public sealed record PrivateBookImportParseResult(
    PrivateLibraryResultStatus Status,
    PrivateBookImportDraft? Draft);

public interface IPrivateBookImportParser
{
    Task<PrivateBookImportParseResult> ParseAsync(
        string fileName,
        string? contentType,
        Stream content,
        CancellationToken cancellationToken = default);
}

public interface IPrivateLibraryContentService
{
    Task<PrivateLibraryOperationResult<PrivateBookImportView>> ImportAsync(
        Guid userId,
        string fileName,
        string? contentType,
        Stream content,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PrivateChapterView>> ListChaptersAsync(
        Guid userId,
        Guid privateBookId,
        CancellationToken cancellationToken = default);

    Task<PrivateChapterContentView?> GetChapterAsync(
        Guid userId,
        Guid privateBookId,
        Guid privateChapterId,
        CancellationToken cancellationToken = default);

    Task<PrivateLibraryOperationResult<PrivateLibraryExport>> ExportAsync(
        Guid userId,
        Guid privateBookId,
        string? format,
        CancellationToken cancellationToken = default);
}
