namespace InkFlow.Modules.Sources.Domain;

/// <summary>
/// 来源侧书目聚合：(SourceId, ExternalBookId) 唯一定位一本书的"来源视图"。
/// 不变量：
/// 1. 章节以 ExternalChapterId 幂等同步——已存在的章节不改动、不重排；
/// 2. 新章节按同步顺序追加 Index（目录快照顺序即抓取顺序）；
/// 3. 元数据可随上游变化更新，但 Id 与外部标识永不改变。
/// </summary>
public sealed class SourceBook
{
    private readonly List<SourceChapter> _chapters = [];

    public Guid Id { get; private set; }
    public string SourceId { get; private set; } = null!;
    public string ExternalBookId { get; private set; } = null!;
    public string Title { get; private set; } = null!;
    public string Author { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public IReadOnlyList<SourceChapter> Chapters => _chapters;

    private SourceBook() { }

    public static SourceBook Create(
        string sourceId, string externalBookId, string title, string author, DateTimeOffset now)
    {
        ValidateIds(sourceId, externalBookId);
        ValidateMetadata(title, author);

        return new SourceBook
        {
            Id = Guid.NewGuid(),
            SourceId = sourceId,
            ExternalBookId = externalBookId,
            Title = title.Trim(),
            Author = author.Trim(),
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    /// <summary>测试/重建用完整构造。</summary>
    public static SourceBook Rehydrate(
        Guid id, string sourceId, string externalBookId, string title, string author,
        DateTimeOffset createdAt, DateTimeOffset updatedAt, IEnumerable<SourceChapter> chapters)
    {
        var book = new SourceBook
        {
            Id = id,
            SourceId = sourceId,
            ExternalBookId = externalBookId,
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

    /// <summary>
    /// 按 ExternalChapterId 幂等同步章节：已存在的不动，新章节按给定顺序追加。
    /// 返回本次新增的章节。重复的外部 ID 在同批次内也只保留首次出现。
    /// </summary>
    public IReadOnlyList<SourceChapter> SyncChapters(
        IEnumerable<(string ExternalChapterId, string Title)> entries, DateTimeOffset now)
    {
        var known = _chapters.Select(c => c.ExternalChapterId).ToHashSet(StringComparer.Ordinal);
        var added = new List<SourceChapter>();
        var seenInBatch = new HashSet<string>(StringComparer.Ordinal);
        var startIndex = _chapters.Count;

        foreach (var (externalId, title) in entries)
        {
            if (string.IsNullOrWhiteSpace(externalId) || !known.Add(externalId) || !seenInBatch.Add(externalId))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(title))
            {
                continue;
            }

            var chapter = new SourceChapter(Guid.NewGuid(), Id, externalId, startIndex + added.Count, title.Trim());
            _chapters.Add(chapter);
            added.Add(chapter);
        }

        if (added.Count > 0)
        {
            UpdatedAt = now;
        }

        return added;
    }

    private static void ValidateIds(string sourceId, string externalBookId)
    {
        if (string.IsNullOrWhiteSpace(sourceId))
        {
            throw new ArgumentException("source id must not be empty.", nameof(sourceId));
        }

        if (string.IsNullOrWhiteSpace(externalBookId))
        {
            throw new ArgumentException("external book id must not be empty.", nameof(externalBookId));
        }
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
