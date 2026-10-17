using InkFlow.Modules.Library.Domain;

namespace InkFlow.Modules.Library.Application;

/// <summary>私有书目的创建、更新与删除命令。</summary>
internal sealed class PrivateLibraryCommands(
    IPrivateBookRepository repository,
    TimeProvider clock)
{
    public async Task<PrivateLibraryOperationResult<PrivateBookView>> CreateAsync(
        Guid userId,
        string? title,
        string? author,
        CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty)
        {
            return Invalid<PrivateBookView>();
        }

        try
        {
            var book = PrivateBook.Create(userId, title ?? string.Empty, author, clock.GetUtcNow());
            await repository.AddAsync(book, cancellationToken).ConfigureAwait(false);
            return Success(PrivateLibraryViewMapper.ToView(book));
        }
        catch (ArgumentException)
        {
            return Invalid<PrivateBookView>();
        }
    }

    public async Task<PrivateLibraryOperationResult<PrivateBookView>> UpdateAsync(
        Guid userId,
        Guid privateBookId,
        string? title,
        string? author,
        long expectedVersion,
        CancellationToken cancellationToken)
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

        return Success(PrivateLibraryViewMapper.ToView(PrivateBook.Rehydrate(
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
        CancellationToken cancellationToken)
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

    private static PrivateLibraryOperationResult<T> Success<T>(T value) =>
        new(PrivateLibraryResultStatus.Success, value);

    private static PrivateLibraryOperationResult<T> NotFound<T>() =>
        new(PrivateLibraryResultStatus.NotFound, default);

    private static PrivateLibraryOperationResult<T> Invalid<T>() =>
        new(PrivateLibraryResultStatus.InvalidRequest, default);

    private static PrivateLibraryOperationResult<T> Conflict<T>() =>
        new(PrivateLibraryResultStatus.Conflict, default);
}
