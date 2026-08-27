using InkFlow.Modules.Content.Application;

namespace InkFlow.Modules.Legado.Application;

/// <summary>搜索结果条目(阅读 3.0 ruleSearch 期望的字段形态)。</summary>
public sealed record LegadoSearchItem(Guid BookId, string Title, string Author, string DetailUrl);

/// <summary>书籍详情(ruleBookInfo)。</summary>
public sealed record LegadoBookInfo(Guid BookId, string Title, string Author, string TocUrl);

/// <summary>目录条目(ruleToc)。</summary>
public sealed record LegadoTocItem(Guid ChapterId, int Index, string Title, string ChapterUrl);

/// <summary>章节正文(ruleContent)。</summary>
public sealed record LegadoContent(Guid ChapterId, string Title, string Content);

public static class LegadoRoutePrefixes
{
    public const string Public = "/api/legado/v1";
    public const string Personal = "/api/legado/v1/personal";
}

/// <summary>
/// Legado v1 只读契约:把正典目录/内容翻译为阅读 3.0 期望的 JSON 形态。
/// 全部端点只读已落库数据——Legado 阅读路径同样零实时抓取。
/// </summary>
public sealed class LegadoContractService(CatalogQueryService catalog)
{
    /// <summary>按关键词过滤书名/作者(v1 内存过滤;全文检索属后续阶段)。空关键词返回全部书目。</summary>
    public Task<IReadOnlyList<LegadoSearchItem>> SearchAsync(
        string query, CancellationToken cancellationToken = default)
        => SearchAsync(query, LegadoRoutePrefixes.Public, cancellationToken);

    public Task<IReadOnlyList<LegadoSearchItem>> SearchAsync(
        string query,
        string routePrefix,
        CancellationToken cancellationToken = default)
    {
        ValidateRoutePrefix(routePrefix);

        // 复用只读查询服务的统一过滤语义,Legado 与公共 API/Reader 保持一致。
        return MapAsync(catalog.SearchBooksAsync(query, cancellationToken), routePrefix);

        static async Task<IReadOnlyList<LegadoSearchItem>> MapAsync(
            Task<IReadOnlyList<BookListItem>> booksTask,
            string routePrefix)
        {
            var books = await booksTask.ConfigureAwait(false);
            return books
                .Select(b => new LegadoSearchItem(
                    b.Id, b.Title, b.Author,
                    $"{routePrefix}/books/{b.Id}"))
                .ToList();
        }
    }

    public async Task<LegadoBookInfo?> GetBookAsync(Guid bookId, CancellationToken cancellationToken = default)
        => await GetBookAsync(bookId, LegadoRoutePrefixes.Public, cancellationToken).ConfigureAwait(false);

    public async Task<LegadoBookInfo?> GetBookAsync(
        Guid bookId,
        string routePrefix,
        CancellationToken cancellationToken = default)
    {
        ValidateRoutePrefix(routePrefix);
        var book = await catalog.GetBookAsync(bookId, cancellationToken).ConfigureAwait(false);
        return book is null
            ? null
            : new LegadoBookInfo(
                book.Id,
                book.Title,
                book.Author,
                $"{routePrefix}/books/{book.Id}/chapters");
    }

    public async Task<IReadOnlyList<LegadoTocItem>?> GetTocAsync(Guid bookId, CancellationToken cancellationToken = default)
        => await GetTocAsync(bookId, LegadoRoutePrefixes.Public, cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<LegadoTocItem>?> GetTocAsync(
        Guid bookId,
        string routePrefix,
        CancellationToken cancellationToken = default)
    {
        ValidateRoutePrefix(routePrefix);
        var book = await catalog.GetBookAsync(bookId, cancellationToken).ConfigureAwait(false);
        return book is null
            ? null
            : book.Chapters
                .Select(c => new LegadoTocItem(
                    c.ChapterId, c.Index, c.Title,
                    $"{routePrefix}/chapters/{c.ChapterId}"))
                .ToList();
    }

    public async Task<LegadoContent?> GetChapterContentAsync(
        Guid chapterId, CancellationToken cancellationToken = default)
        => await GetChapterContentAsync(
            chapterId,
            LegadoRoutePrefixes.Public,
            cancellationToken).ConfigureAwait(false);

    public async Task<LegadoContent?> GetChapterContentAsync(
        Guid chapterId,
        string routePrefix,
        CancellationToken cancellationToken = default)
    {
        ValidateRoutePrefix(routePrefix);
        var content = await catalog.GetChapterContentAsync(chapterId, cancellationToken).ConfigureAwait(false);
        if (content is null)
        {
            return null;
        }

        return new LegadoContent(content.ChapterId, content.Title, string.Join("\n\n", content.Paragraphs));
    }

    private static void ValidateRoutePrefix(string routePrefix)
    {
        if (string.IsNullOrWhiteSpace(routePrefix) ||
            !routePrefix.StartsWith('/') ||
            routePrefix.EndsWith('/') ||
            routePrefix.Any(char.IsWhiteSpace) ||
            routePrefix.Contains('?') ||
            routePrefix.Contains('#'))
        {
            throw new ArgumentException("route prefix must be a normalized path.", nameof(routePrefix));
        }
    }
}
