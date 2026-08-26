namespace InkFlow.Modules.Library.Domain;

/// <summary>
/// 章节映射：来源章节 → 正典章节的稳定绑定。
/// 不变量：
/// 1. 同一 (SourceId, ExternalChapterId) 至多一条映射；
/// 2. 映射一经创建不可改指向——阅读进度与对外 ChapterId 都依赖它稳定。
/// </summary>
public sealed record ChapterMapping(
    Guid Id,
    string SourceId,
    string ExternalChapterId,
    Guid SourceChapterId,
    Guid CanonicalBookId,
    Guid CanonicalChapterId,
    DateTimeOffset CreatedAt);
