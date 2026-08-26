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
/// 公共目录/阅读查询服务(只读)。全部数据来自已落库的正典书目与 IsCurrent 内容版本——
/// 普通阅读路径零实时抓取(架构不变量 3)。
/// </summary>
public sealed class CatalogQueryService(
    ICanonicalBookRepository bookRepository,
    IContentVersionRepository versionRepository)
{
    public async Task<IReadOnlyList<BookListItem>> ListBooksAsync(CancellationToken cancellationToken = default)
    {
        var books = await bookRepository.ListAsync(cancellationToken).ConfigureAwait(false);

        // 列表页不含章节,章节数以聚合当前状态为准(v1 简化:逐本加载)。
        var items = new List<BookListItem>(books.Count);
        foreach (var book in books)
        {
            var full = await bookRepository.GetAsync(book.Id, cancellationToken).ConfigureAwait(false);
            items.Add(new BookListItem(book.Id, book.Title, book.Author, full?.Chapters.Count ?? 0));
        }

        return items;
    }

    public async Task<BookDetail?> GetBookAsync(Guid bookId, CancellationToken cancellationToken = default)
    {
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
        var version = await versionRepository
            .GetCurrentForChapterAsync(chapterId, cancellationToken)
            .ConfigureAwait(false);

        if (version is null)
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
