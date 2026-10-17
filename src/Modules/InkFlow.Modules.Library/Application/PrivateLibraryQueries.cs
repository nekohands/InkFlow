using InkFlow.Modules.Library.Domain;

namespace InkFlow.Modules.Library.Application;

/// <summary>私有书目的用户范围读取。</summary>
internal sealed class PrivateLibraryQueries(IPrivateBookRepository repository)
{
    public async Task<IReadOnlyList<PrivateBookView>> ListAsync(
        Guid userId,
        int limit,
        CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty)
        {
            return [];
        }

        var boundedLimit = Math.Clamp(limit, 1, PrivateLibraryService.MaxPageSize);
        var books = await repository.ListAsync(userId, boundedLimit, cancellationToken)
            .ConfigureAwait(false);
        return books.Select(PrivateLibraryViewMapper.ToView).ToList();
    }

    public async Task<PrivateBookView?> GetAsync(
        Guid userId,
        Guid privateBookId,
        CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty || privateBookId == Guid.Empty)
        {
            return null;
        }

        var book = await repository.GetAsync(userId, privateBookId, cancellationToken)
            .ConfigureAwait(false);
        return book is null ? null : PrivateLibraryViewMapper.ToView(book);
    }
}
