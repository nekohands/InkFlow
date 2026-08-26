namespace InkFlow.Modules.Library.Domain;

/// <summary>
/// 正典章节：对外稳定的 ChapterId 承载者。
/// ContentVersion 尚未引入前，本实体只承载目录信息（标题与阅读序号）。
/// </summary>
public sealed record CanonicalChapter(
    Guid Id,
    Guid BookId,
    int Index,
    string Title,
    DateTimeOffset CreatedAt);
