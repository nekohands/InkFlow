namespace InkFlow.Modules.Content.Domain;

/// <summary>
/// 内容版本:某正典章节在某一时刻的规范化内容快照。
/// 不变量:
/// 1. 版本一经创建不可修改(新内容 = 新版本);
/// 2. 同一章节下 CanonicalHash 唯一(相同规范化内容不会产生第二个版本);
/// 3. 每章节至多一个 IsCurrent 版本,选优规则由发布服务执行。
/// </summary>
public sealed class ContentVersion
{
    public Guid Id { get; private set; }
    public Guid CanonicalBookId { get; private set; }
    public Guid CanonicalChapterId { get; private set; }
    public string SourceId { get; private set; } = null!;
    public string CanonicalHash { get; private set; } = null!;
    public string CanonicalText { get; private set; } = null!;
    public int ParagraphCount { get; private set; }
    public int QualityScore { get; private set; }
    public string QualityAlgorithmVersion { get; private set; } = QualityEngine.AlgorithmVersion;
    public string QualityEvidence { get; private set; } = null!;
    public bool IsCurrent { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private ContentVersion() { }

    public static ContentVersion Create(
        Guid canonicalBookId, Guid canonicalChapterId, string sourceId,
        ContentDocument document, DateTimeOffset now)
    {
        if (document.Paragraphs.Count == 0)
        {
            throw new ArgumentException("cannot publish an empty content document.", nameof(document));
        }

        var (score, evidence) = QualityEngine.Evaluate(document);

        return new ContentVersion
        {
            Id = Guid.NewGuid(),
            CanonicalBookId = canonicalBookId,
            CanonicalChapterId = canonicalChapterId,
            SourceId = sourceId,
            CanonicalHash = QualityEngine.ComputeCanonicalHash(document),
            CanonicalText = document.CanonicalText,
            ParagraphCount = document.Paragraphs.Count,
            QualityScore = score,
            QualityAlgorithmVersion = QualityEngine.AlgorithmVersion,
            QualityEvidence = evidence.Describe(),
            IsCurrent = false, // 由发布服务统一选优
            CreatedAt = now,
        };
    }

    /// <summary>测试/重建用完整构造。</summary>
    public static ContentVersion Rehydrate(
        Guid id, Guid canonicalBookId, Guid canonicalChapterId, string sourceId,
        string canonicalHash, string canonicalText, int paragraphCount,
        int qualityScore, bool isCurrent, DateTimeOffset createdAt,
        string? qualityAlgorithmVersion = null, string? qualityEvidence = null) =>
        new()
        {
            Id = id,
            CanonicalBookId = canonicalBookId,
            CanonicalChapterId = canonicalChapterId,
            SourceId = sourceId,
            CanonicalHash = canonicalHash,
            CanonicalText = canonicalText,
            ParagraphCount = paragraphCount,
            QualityScore = qualityScore,
            QualityAlgorithmVersion = string.IsNullOrWhiteSpace(qualityAlgorithmVersion)
                ? QualityEngine.AlgorithmVersion
                : qualityAlgorithmVersion,
            QualityEvidence = string.IsNullOrWhiteSpace(qualityEvidence)
                ? "legacy"
                : qualityEvidence,
            IsCurrent = isCurrent,
            CreatedAt = createdAt,
        };

    /// <summary>选优:质量分高者胜;平分取创建时间较新者。</summary>
    public static ContentVersion SelectCurrent(ContentVersion a, ContentVersion b) =>
        (a.QualityScore, a.CreatedAt) is var (scoreA, createdA) &&
        (b.QualityScore > scoreA || (b.QualityScore == scoreA && b.CreatedAt >= createdA))
            ? b
            : a;
}
