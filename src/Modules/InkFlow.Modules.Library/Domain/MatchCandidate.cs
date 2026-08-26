namespace InkFlow.Modules.Library.Domain;

public enum MatchCandidateStatus
{
    /// <summary>待人工或自动确认。</summary>
    Pending,
    /// <summary>已确认映射：该来源书正式挂接到正典书。</summary>
    Confirmed,
    /// <summary>已否决：此来源书与该正典书的匹配被否决（可重新提出新候选）。</summary>
    Rejected,
}

/// <summary>
/// 匹配候选：一条"来源书目 → 正典书目"的映射记录。
/// 不变量：
/// 1. 同一 (SourceId, ExternalBookId) 至多存在一条候选——重复导入复用既有记录；
/// 2. Confirmed 候选不可改指向另一本正典书（对外 BookId 稳定）；换绑 = 否决旧候选 + 新建。
/// </summary>
public sealed record MatchCandidate(
    Guid Id,
    Guid CanonicalBookId,
    string SourceId,
    string ExternalBookId,
    MatchCandidateStatus Status,
    DateTimeOffset CreatedAt)
{
    public static MatchCandidate Confirm(Guid canonicalBookId, string sourceId, string externalBookId, DateTimeOffset now) =>
        new(Guid.NewGuid(), canonicalBookId, sourceId, externalBookId, MatchCandidateStatus.Confirmed, now);
}
