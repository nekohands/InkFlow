namespace InkFlow.Modules.Library.Infrastructure.Persistence;

/// <summary>match_candidates 表实体。</summary>
public sealed class MatchCandidateEntity
{
    public Guid Id { get; set; }
    public Guid CanonicalBookId { get; set; }
    public string SourceId { get; set; } = null!;
    public string ExternalBookId { get; set; } = null!;
    public int Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
