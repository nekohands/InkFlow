namespace InkFlow.Modules.Library.Domain;

/// <summary>章节对齐算法的版本标识；算法变化必须产生新的版本值。</summary>
public static class ChapterAlignmentAlgorithm
{
    public const string Version = "chapter-alignment-v1";
}

/// <summary>
/// 章节映射：来源章节 → 正典章节的稳定绑定。
/// 不变量：
/// 1. 同一 (SourceId, ExternalChapterId) 至多一条映射；
/// 2. 映射一经创建不可改指向——阅读进度与对外 ChapterId 都依赖它稳定。
/// 3. 映射保留对齐算法版本与证据，便于解释、审计和回放。
/// </summary>
public sealed record ChapterMapping(
    Guid Id,
    string SourceId,
    string ExternalChapterId,
    Guid SourceChapterId,
    Guid CanonicalBookId,
    Guid CanonicalChapterId,
    DateTimeOffset CreatedAt,
    string AlignmentAlgorithmVersion = ChapterAlignmentAlgorithm.Version,
    string AlignmentEvidence = "legacy");
