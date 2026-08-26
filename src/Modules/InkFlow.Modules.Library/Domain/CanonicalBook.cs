namespace InkFlow.Modules.Library.Domain;

/// <summary>
/// 正典书籍聚合：对外稳定 BookId 的唯一来源。
/// 不变量：
/// 1. 章节序号在书内唯一且从 0 连续递增（目录顺序即阅读顺序）；
/// 2. 已发布的章节不可删除、不可改变序号（阅读历史依赖稳定 ID）；
/// 3. SourceBook 到 CanonicalBook 的匹配由 Library 拥有，Sources/Crawling 模块不得反向持有。
/// </summary>
public sealed class CanonicalBook
{
    private readonly List<CanonicalChapter> _chapters = [];

    public Guid Id { get; private set; }
    public string Title { get; private set; } = null!;
    public string Author { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public IReadOnlyList<CanonicalChapter> Chapters => _chapters;

    private CanonicalBook() { }

    public static CanonicalBook Create(string title, string author, DateTimeOffset now)
    {
        ValidateMetadata(title, author);
        return new CanonicalBook
        {
            Id = Guid.NewGuid(),
            Title = title.Trim(),
            Author = author.Trim(),
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    /// <summary>测试/重建用完整构造。</summary>
    public static CanonicalBook Rehydrate(
        Guid id, string title, string author, DateTimeOffset createdAt, DateTimeOffset updatedAt,
        IEnumerable<CanonicalChapter> chapters)
    {
        var book = new CanonicalBook
        {
            Id = id,
            Title = title,
            Author = author,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt,
        };
        book._chapters.AddRange(chapters.OrderBy(c => c.Index));
        return book;
    }

    public void UpdateMetadata(string title, string author, DateTimeOffset now)
    {
        ValidateMetadata(title, author);
        Title = title.Trim();
        Author = author.Trim();
        UpdatedAt = now;
    }

    /// <summary>追加章节。序号必须等于当前最大序号 + 1（目录只能追加，不能插队或改号）。</summary>
    public CanonicalChapter AddChapter(int index, string title, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("chapter title must not be empty.", nameof(title));
        }

        var expected = _chapters.Count == 0 ? 0 : _chapters.Max(c => c.Index) + 1;
        if (index != expected)
        {
            throw new InvalidOperationException(
                $"chapter index must be {expected} (append-only), got {index} (book {Id}).");
        }

        var chapter = new CanonicalChapter(Guid.NewGuid(), Id, index, title.Trim(), now);
        _chapters.Add(chapter);
        UpdatedAt = now;
        return chapter;
    }

    private static void ValidateMetadata(string title, string author)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("book title must not be empty.", nameof(title));
        }

        if (string.IsNullOrWhiteSpace(author))
        {
            throw new ArgumentException("book author must not be empty.", nameof(author));
        }
    }
}
