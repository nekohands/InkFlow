namespace InkFlow.Modules.Content.Infrastructure.Persistence;

/// <summary>正文当前版本选择审计记录的持久化形态。</summary>
public sealed class ContentSelectionDecisionEntity
{
    public Guid Id { get; set; }
    public Guid CanonicalChapterId { get; set; }
    public Guid SelectedVersionId { get; set; }
    public string AlgorithmVersion { get; set; } = null!;
    public string Evidence { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; }
}
