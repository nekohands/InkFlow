using InkFlow.Modules.Library.Domain;

namespace InkFlow.Modules.Library.Application;

public sealed record CanonicalBookSummary(
    Guid Id,
    string Title,
    string Author,
    int ChapterCount);

public sealed record CanonicalChapterSummary(Guid Id, int Index, string Title);

/// <summary>正典书籍仓储契约。实现负责聚合与实体的映射及章节增量持久化。</summary>
public interface ICanonicalBookRepository
{
    Task AddAsync(CanonicalBook book, CancellationToken cancellationToken = default);

    Task<CanonicalBook?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>按书籍 ID 读取轻量书籍摘要，不加载章节集合。</summary>
    async Task<CanonicalBookSummary?> GetSummaryAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var book = await GetAsync(id, cancellationToken).ConfigureAwait(false);
        return book is null
            ? null
            : new CanonicalBookSummary(book.Id, book.Title, book.Author, book.Chapters.Count);
    }

    /// <summary>按书籍和章节稳定 ID 读取单章目录元数据，不加载整本书聚合。</summary>
    async Task<CanonicalChapter?> GetChapterAsync(
        Guid bookId,
        Guid chapterId,
        CancellationToken cancellationToken = default)
    {
        var book = await GetAsync(bookId, cancellationToken).ConfigureAwait(false);
        return book?.Chapters.FirstOrDefault(chapter => chapter.Id == chapterId);
    }

    /// <summary>按书籍 ID 读取章节目录投影，不加载整本书聚合。</summary>
    async Task<IReadOnlyList<CanonicalChapterSummary>> ListChapterSummariesAsync(
        Guid bookId,
        CancellationToken cancellationToken = default)
    {
        var book = await GetAsync(bookId, cancellationToken).ConfigureAwait(false);
        return book?.Chapters
            .Select(chapter => new CanonicalChapterSummary(chapter.Id, chapter.Index, chapter.Title))
            .ToList() ?? [];
    }

    /// <summary>按正典书 ID 批量读取轻量书名，供跨模块列表投影使用。</summary>
    async Task<IReadOnlyDictionary<Guid, string>> GetTitlesAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default)
    {
        var titles = new Dictionary<Guid, string>();
        foreach (var id in ids.Distinct())
        {
            var book = await GetAsync(id, cancellationToken).ConfigureAwait(false);
            if (book is not null)
            {
                titles[id] = book.Title;
            }
        }

        return titles;
    }

    /// <summary>全部书目(不含章节,供列表页使用)。</summary>
    Task<IReadOnlyList<CanonicalBook>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>有界读取书目列表摘要；生产实现应在单个投影查询中计算章节数。</summary>
    async Task<IReadOnlyList<CanonicalBookSummary>> ListSummariesAsync(
        int limit,
        CancellationToken cancellationToken = default)
    {
        var books = await ListAsync(cancellationToken).ConfigureAwait(false);
        var summaries = new List<CanonicalBookSummary>(Math.Min(Math.Max(limit, 1), 100));
        foreach (var book in books.Take(Math.Clamp(limit, 1, 100)))
        {
            var full = await GetAsync(book.Id, cancellationToken).ConfigureAwait(false);
            summaries.Add(new CanonicalBookSummary(
                book.Id,
                book.Title,
                book.Author,
                full?.Chapters.Count ?? 0));
        }

        return summaries;
    }

    /// <summary>按书名或作者筛选有界书目摘要；生产实现应在查询中完成筛选。</summary>
    async Task<IReadOnlyList<CanonicalBookSummary>> SearchSummariesAsync(
        string query,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var keyword = query.Trim();
        var summaries = await ListSummariesAsync(
            keyword.Length == 0 ? limit : 100,
            cancellationToken).ConfigureAwait(false);
        if (keyword.Length == 0)
        {
            return summaries;
        }

        return summaries
            .Where(book => book.Title.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                          || book.Author.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            .Take(Math.Clamp(limit, 1, 100))
            .ToList();
    }

    /// <summary>
    /// 按归一化的书名+作者查找已有正典书(Book Matcher v1 的同书识别依据);
    /// 未命中返回 null。
    /// </summary>
    Task<CanonicalBook?> FindByTitleAuthorAsync(
        string title, string author, CancellationToken cancellationToken = default);

    /// <summary>
    /// 归一化 (title, author) 维度的匹配互斥作用域：生产实现必须在单个事务内
    /// 取稳定 advisory lock 并保持到提交，使锁内的候选复查与书/候选写入成为
    /// 原子临界区，杜绝并发匹配创建重复正典身份。默认实现为无互斥的兼容回退，
    /// 仅供测试替身使用；生产实现必须覆写。
    /// </summary>
    Task<ICanonicalMatchScope> BeginTitleAuthorScopeAsync(
        string title, string author, CancellationToken cancellationToken = default)
        => Task.FromResult<ICanonicalMatchScope>(NoOpCanonicalMatchScope.Instance);

    /// <summary>写回聚合的元数据与新增章节（已有章节不可变）。</summary>
    Task SaveAsync(CanonicalBook book, CancellationToken cancellationToken = default);
}

/// <summary>
/// 匹配临界区作用域：同一 DbContext 上的全部仓储写入共享作用域事务。
/// 提交后互斥锁随事务释放；未提交即释放（Dispose）时回滚全部临界区写入。
/// </summary>
public interface ICanonicalMatchScope : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken = default);
}

internal sealed class NoOpCanonicalMatchScope : ICanonicalMatchScope
{
    internal static readonly NoOpCanonicalMatchScope Instance = new();

    public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
