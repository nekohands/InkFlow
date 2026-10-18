using InkFlow.Modules.Content.Application;
using InkFlow.Modules.Library.Application;

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
    private readonly CatalogBookReader bookReader =
        new(bookRepository, policyReader);

    private readonly CatalogContentReader contentReader =
        new(bookRepository, versionRepository, policyReader, selectionService);

    public const int DefaultPageSize = CatalogBookReader.DefaultPageSize;
    public const int MaxPageSize = CatalogBookReader.MaxPageSize;

    public Task<IReadOnlyList<BookListItem>> ListBooksAsync(
        CancellationToken cancellationToken = default) =>
        bookReader.ListAsync(cancellationToken);

    public Task<IReadOnlyList<BookListItem>> ListBooksAsync(
        int limit,
        CancellationToken cancellationToken = default) =>
        bookReader.ListAsync(limit, cancellationToken);

    /// <summary>
    /// 按关键词过滤落库正典书目:书名或作者大小写不敏感包含匹配(v1 简单查询过滤;
    /// 全文检索属后续阶段)。空白关键词返回有界书目列表(浏览语义)。
    /// 数据一律来自已落库 Canonical 数据——本服务不负责触发来源发现,
    /// 发现编排由调用方(BookDiscoveryService)先行完成。
    /// </summary>
    public Task<IReadOnlyList<BookListItem>> SearchBooksAsync(
        string query,
        CancellationToken cancellationToken = default) =>
        bookReader.SearchAsync(query, cancellationToken);

    public Task<IReadOnlyList<BookListItem>> SearchBooksAsync(
        string query,
        int limit,
        CancellationToken cancellationToken = default) =>
        bookReader.SearchAsync(query, limit, cancellationToken);

    public Task<BookListItem?> GetBookSummaryAsync(
        Guid bookId,
        CancellationToken cancellationToken = default) =>
        bookReader.GetSummaryAsync(bookId, cancellationToken);

    public Task<BookDetail?> GetBookAsync(
        Guid bookId,
        CancellationToken cancellationToken = default) =>
        bookReader.GetAsync(bookId, cancellationToken);

    public Task<IReadOnlyList<ChapterListItem>?> GetChapterListAsync(
        Guid bookId,
        CancellationToken cancellationToken = default) =>
        bookReader.ListChaptersAsync(bookId, cancellationToken);

    /// <summary>读取章节正文:返回当前版本的规范化段落。未发布内容时返回 null。</summary>
    public Task<ChapterContent?> GetChapterContentAsync(
        Guid chapterId, CancellationToken cancellationToken = default) =>
        contentReader.ReadAsync(chapterId, cancellationToken);
}
