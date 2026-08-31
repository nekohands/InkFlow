using InkFlow.Modules.Content.Application;
using InkFlow.Modules.Library.Application;
using InkFlow.Modules.Library.Domain;

namespace InkFlow.Modules.Content.Application;

public sealed record BookListItem(
    Guid Id, string Title, string Author, int ChapterCount);

public sealed record BookDetail(
    Guid Id, string Title, string Author,
    IReadOnlyList<ChapterListItem> Chapters);

public sealed record ChapterListItem(Guid ChapterId, int Index, string Title);

public sealed record ChapterContent(
    Guid ChapterId, Guid BookId, int Index, string Title,
    string SourceId, IReadOnlyList<string> Paragraphs);

/// <summary>
/// 公共目录/阅读查询服务。全部数据来自已落库的正典书目与 ContentVersion——
/// 正文读取前可在已落库的候选与来源健康状态中重选当前版本，但绝不实时抓取第三方。
/// </summary>
public sealed class CatalogQueryService(
    ICanonicalBookRepository bookRepository,
    IContentVersionRepository versionRepository,
    IContentPolicyReader policyReader,
    IContentSelectionService? selectionService = null)
{
    public async Task<IReadOnlyList<BookListItem>> ListBooksAsync(CancellationToken cancellationToken = default)
    {
        var books = await bookRepository.ListAsync(cancellationToken).ConfigureAwait(false);

        // 列表页不含章节,章节数以聚合当前状态为准(v1 简化:逐本加载)。
        var items = new List<BookListItem>(books.Count);
        foreach (var book in books)
        {
            if (await policyReader
                .IsTakedownAsync(book.Id, cancellationToken)
                .ConfigureAwait(false))
            {
                continue;
            }

            var full = await bookRepository.GetAsync(book.Id, cancellationToken).ConfigureAwait(false);
            items.Add(new BookListItem(book.Id, book.Title, book.Author, full?.Chapters.Count ?? 0));
        }

        return items;
    }

    /// <summary>
    /// 按关键词过滤落库正典书目:书名或作者大小写不敏感包含匹配(v1 内存过滤;
    /// 全文检索属后续阶段)。空白关键词返回全部书目(浏览语义)。
    /// 数据一律来自已落库 Canonical 数据——本服务不负责触发来源发现,
    /// 发现编排由调用方(BookDiscoveryService)先行完成。
    /// </summary>
    public async Task<IReadOnlyList<BookListItem>> SearchBooksAsync(
        string query, CancellationToken cancellationToken = default)
    {
        var books = await ListBooksAsync(cancellationToken).ConfigureAwait(false);

        var keyword = query?.Trim() ?? string.Empty;
        if (keyword.Length == 0)
        {
            return books;
        }

        return books
            .Where(b => b.Title.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                        || b.Author.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    public async Task<BookDetail?> GetBookAsync(Guid bookId, CancellationToken cancellationToken = default)
    {
        if (await policyReader
            .IsTakedownAsync(bookId, cancellationToken)
            .ConfigureAwait(false))
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

    /// <summary>读取章节正文:返回当前版本的规范化段落。未发布内容时返回 null。</summary>
    public async Task<ChapterContent?> GetChapterContentAsync(
        Guid chapterId, CancellationToken cancellationToken = default)
    {
        // 先读取不含正文的关联书籍 ID，策略拒绝时不加载正文列。
        var canonicalBookId = await versionRepository
            .GetCurrentCanonicalBookIdAsync(chapterId, cancellationToken)
            .ConfigureAwait(false);

        if (canonicalBookId is null || await policyReader
            .IsTakedownAsync(canonicalBookId.Value, cancellationToken)
            .ConfigureAwait(false))
        {
            return null;
        }

        // 健康状态变化后，第一次正文读取负责在已落库候选中完成重选。
        // 该步骤不访问任何来源 URL；选择失败不应绕过后续的可见性门控。
        if (selectionService is not null)
        {
            var selection = await selectionService
                .SelectCurrentAsync(chapterId, cancellationToken)
                .ConfigureAwait(false);
            if (!selection.IsSuccess)
            {
                return null;
            }

            canonicalBookId = await versionRepository
                .GetCurrentCanonicalBookIdAsync(chapterId, cancellationToken)
                .ConfigureAwait(false);
            if (canonicalBookId is null || await policyReader
                .IsTakedownAsync(canonicalBookId.Value, cancellationToken)
                .ConfigureAwait(false))
            {
                return null;
            }
        }

        var version = await versionRepository
            .GetCurrentForChapterAsync(chapterId, cancellationToken)
            .ConfigureAwait(false);

        if (version is null)
        {
            return null;
        }

        // 防止元数据检查与正文加载之间发生下架时把正文返回给调用方。
        if (await policyReader
            .IsTakedownAsync(version.CanonicalBookId, cancellationToken)
            .ConfigureAwait(false))
        {
            return null;
        }

        var book = await bookRepository
            .GetAsync(version.CanonicalBookId, cancellationToken)
            .ConfigureAwait(false);

        var chapter = book?.Chapters.FirstOrDefault(c => c.Id == chapterId);

        var paragraphs = version.CanonicalText
            .Split("\n\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return new ChapterContent(
            chapterId,
            version.CanonicalBookId,
            chapter?.Index ?? 0,
            chapter?.Title ?? string.Empty,
            version.SourceId,
            paragraphs);
    }
}
