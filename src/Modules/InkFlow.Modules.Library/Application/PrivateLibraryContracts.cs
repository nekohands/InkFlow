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
