namespace InkFlow.Modules.Content.Domain;

/// <summary>当前正文选择算法的稳定版本标识和审计证据上限。</summary>
public static class ContentSelectionAlgorithm
{
    public const string Version = "content-selection-v1";
    public const int MaxEvidenceLength = 2048;
}

/// <summary>
/// 某正典章节一次当前正文选择的不可变审计记录。
/// 选择结果可重放、可解释；它不改变 ContentVersion 的历史快照。
/// </summary>
public sealed class ContentSelectionDecision
{
    public Guid Id { get; private set; }
    public Guid CanonicalChapterId { get; private set; }
    public Guid SelectedVersionId { get; private set; }
    public string AlgorithmVersion { get; private set; } = ContentSelectionAlgorithm.Version;
    public string Evidence { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; }

    private ContentSelectionDecision() { }

    public static ContentSelectionDecision Create(
        Guid canonicalChapterId,
        Guid selectedVersionId,
        string evidence,
        DateTimeOffset createdAt) =>
        new()
        {
            Id = Guid.NewGuid(),
            CanonicalChapterId = canonicalChapterId,
            SelectedVersionId = selectedVersionId,
            AlgorithmVersion = ContentSelectionAlgorithm.Version,
            Evidence = NormalizeEvidence(evidence),
            CreatedAt = createdAt,
        };

    public static ContentSelectionDecision Rehydrate(
        Guid id,
        Guid canonicalChapterId,
        Guid selectedVersionId,
        string? algorithmVersion,
        string? evidence,
        DateTimeOffset createdAt) =>
        new()
        {
            Id = id,
            CanonicalChapterId = canonicalChapterId,
            SelectedVersionId = selectedVersionId,
            AlgorithmVersion = string.IsNullOrWhiteSpace(algorithmVersion)
                ? ContentSelectionAlgorithm.Version
                : algorithmVersion,
            Evidence = NormalizeEvidence(evidence),
            CreatedAt = createdAt,
        };

    private static string NormalizeEvidence(string? evidence)
    {
        var value = string.IsNullOrWhiteSpace(evidence) ? "legacy" : evidence.Trim();
        return value.Length <= ContentSelectionAlgorithm.MaxEvidenceLength
            ? value
            : value[..ContentSelectionAlgorithm.MaxEvidenceLength];
    }
}
