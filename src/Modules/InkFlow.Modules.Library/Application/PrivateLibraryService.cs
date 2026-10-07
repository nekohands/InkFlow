using InkFlow.Modules.Library.Domain;

namespace InkFlow.Modules.Library.Application;

public sealed class PrivateLibraryService(
    IPrivateBookRepository repository,
    TimeProvider clock) : IPrivateLibraryService
{
    public const int DefaultPageSize = 50;
    public const int MaxPageSize = 100;

    public async Task<PrivateLibraryOperationResult<PrivateBookView>> CreateAsync(
        Guid userId,
        string? title,
        string? author,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            return Invalid<PrivateBookView>();
        }

        try
        {
            var book = PrivateBook.Create(userId, title ?? string.Empty, author, clock.GetUtcNow());
            await repository.AddAsync(book, cancellationToken).ConfigureAwait(false);
            return Success(ToView(book));
        }
        catch (ArgumentException)
        {
            return Invalid<PrivateBookView>();
        }
    }

    public async Task<IReadOnlyList<PrivateBookView>> ListAsync(
        Guid userId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            return [];
        }

        var boundedLimit = Math.Clamp(limit, 1, MaxPageSize);
        var books = await repository.ListAsync(userId, boundedLimit, cancellationToken)
            .ConfigureAwait(false);
        return books.Select(ToView).ToList();
    }

    public async Task<PrivateBookView?> GetAsync(
        Guid userId,
        Guid privateBookId,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty || privateBookId == Guid.Empty)
        {
            return null;
        }

        var book = await repository.GetAsync(userId, privateBookId, cancellationToken)
            .ConfigureAwait(false);
        return book is null ? null : ToView(book);
    }

    public async Task<PrivateLibraryOperationResult<PrivateBookView>> UpdateAsync(
        Guid userId,
        Guid privateBookId,
        string? title,
        string? author,
        long expectedVersion,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty || privateBookId == Guid.Empty || expectedVersion < 1)
        {
            return Invalid<PrivateBookView>();
        }

        var book = await repository.GetAsync(userId, privateBookId, cancellationToken)
            .ConfigureAwait(false);
        if (book is null)
        {
            return NotFound<PrivateBookView>();
        }

        if (book.Version != expectedVersion)
        {
            return Conflict<PrivateBookView>();
        }

        try
        {
            book.UpdateMetadata(title ?? string.Empty, author, clock.GetUtcNow());
        }
        catch (ArgumentException)
        {
            return Invalid<PrivateBookView>();
        }

        var saveStatus = await repository
            .SaveAsync(book, expectedVersion, cancellationToken)
            .ConfigureAwait(false);
        if (saveStatus == PrivateBookSaveStatus.NotFound)
        {
            return NotFound<PrivateBookView>();
        }

        if (saveStatus == PrivateBookSaveStatus.Conflict)
        {
            return Conflict<PrivateBookView>();
        }

        return Success(ToView(PrivateBook.Rehydrate(
            book.UserId,
            book.Id,
            book.Title,
            book.Author,
            book.CreatedAt,
            book.UpdatedAt,
            checked(book.Version + 1))));
    }

    public async Task<PrivateLibraryResultStatus> DeleteAsync(
        Guid userId,
        Guid privateBookId,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty || privateBookId == Guid.Empty)
        {
            return PrivateLibraryResultStatus.NotFound;
        }

        return await repository.DeleteAsync(userId, privateBookId, cancellationToken)
            .ConfigureAwait(false)
            ? PrivateLibraryResultStatus.Success
            : PrivateLibraryResultStatus.NotFound;
    }

    private static PrivateBookView ToView(PrivateBook book) =>
        new(book.Id, book.Title, book.Author, book.CreatedAt, book.UpdatedAt, book.Version);

    private static PrivateLibraryOperationResult<T> Success<T>(T value) =>
        new(PrivateLibraryResultStatus.Success, value);

    private static PrivateLibraryOperationResult<T> NotFound<T>() =>
        new(PrivateLibraryResultStatus.NotFound, default);

    private static PrivateLibraryOperationResult<T> Invalid<T>() =>
        new(PrivateLibraryResultStatus.InvalidRequest, default);

    private static PrivateLibraryOperationResult<T> Conflict<T>() =>
        new(PrivateLibraryResultStatus.Conflict, default);
}
