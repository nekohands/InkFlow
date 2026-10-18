using InkFlow.Modules.Library.Application;

namespace InkFlow.Modules.Content.Application;

/// <summary>
/// 已落库正典书目与章节元数据读取：负责有界列表/搜索、可见性门控和元数据投影，
/// 不读取正文版本，也不触发来源发现。
/// </summary>
internal sealed class CatalogBookReader(
    ICanonicalBookRepository bookRepository,
    IContentPolicyReader policyReader)
{
    public const int DefaultPageSize = 100;
    public const int MaxPageSize = 100;

    public Task<IReadOnlyList<BookListItem>> ListAsync(
        CancellationToken cancellationToken = default) =>
        ListAsync(DefaultPageSize, cancellationToken);

    public async Task<IReadOnlyList<BookListItem>> ListAsync(
        int limit,
        CancellationToken cancellationToken = default)
    {
        var summaries = await bookRepository
            .ListSummariesAsync(Math.Clamp(limit, 1, MaxPageSize), cancellationToken)
            .ConfigureAwait(false);
        return await MapVisibleBooksAsync(summaries, cancellationToken).ConfigureAwait(false);
    }

    public Task<IReadOnlyList<BookListItem>> SearchAsync(
        string query,
        CancellationToken cancellationToken = default) =>
        SearchAsync(query, DefaultPageSize, cancellationToken);

    public async Task<IReadOnlyList<BookListItem>> SearchAsync(
        string query,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var keyword = query?.Trim() ?? string.Empty;
        var summaries = await bookRepository
            .SearchSummariesAsync(keyword, Math.Clamp(limit, 1, MaxPageSize), cancellationToken)
            .ConfigureAwait(false);
        return await MapVisibleBooksAsync(summaries, cancellationToken).ConfigureAwait(false);
    }

    public async Task<BookListItem?> GetSummaryAsync(
        Guid bookId,
        CancellationToken cancellationToken = default)
    {
        if (!await IsVisibleAsync(bookId, cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        var book = await bookRepository
            .GetSummaryAsync(bookId, cancellationToken)
            .ConfigureAwait(false);
        return book is null
            ? null
            : new BookListItem(book.Id, book.Title, book.Author, book.ChapterCount);
    }

    public async Task<BookDetail?> GetAsync(
        Guid bookId,
        CancellationToken cancellationToken = default)
    {
        if (!await IsVisibleAsync(bookId, cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        var book = await bookRepository.GetAsync(bookId, cancellationToken).ConfigureAwait(false);
        if (book is null)
        {
            return null;
        }

        return new BookDetail(
            book.Id,
            book.Title,
            book.Author,
            book.Chapters
                .Select(c => new ChapterListItem(c.Id, c.Index, c.Title))
                .ToList());
    }

    public async Task<IReadOnlyList<ChapterListItem>?> ListChaptersAsync(
        Guid bookId,
        CancellationToken cancellationToken = default)
    {
        if (!await IsVisibleAsync(bookId, cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        if (await bookRepository.GetSummaryAsync(bookId, cancellationToken).ConfigureAwait(false) is null)
        {
            return null;
        }

        var chapters = await bookRepository
            .ListChapterSummariesAsync(bookId, cancellationToken)
            .ConfigureAwait(false);
        return chapters
            .Select(chapter => new ChapterListItem(chapter.Id, chapter.Index, chapter.Title))
            .ToList();
    }

    private async Task<IReadOnlyList<BookListItem>> MapVisibleBooksAsync(
        IReadOnlyList<CanonicalBookSummary> summaries,
        CancellationToken cancellationToken)
    {
        var takenDownBookIds = await policyReader
            .ListTakedownBookIdsAsync(summaries.Select(book => book.Id).ToArray(), cancellationToken)
            .ConfigureAwait(false);

        var items = new List<BookListItem>(summaries.Count);
        foreach (var book in summaries)
        {
            if (!takenDownBookIds.Contains(book.Id))
            {
                items.Add(new BookListItem(book.Id, book.Title, book.Author, book.ChapterCount));
            }
        }

        return items;
    }

    private async Task<bool> IsVisibleAsync(
        Guid bookId,
        CancellationToken cancellationToken) =>
        !await policyReader
            .IsTakedownAsync(bookId, cancellationToken)
            .ConfigureAwait(false);
}
