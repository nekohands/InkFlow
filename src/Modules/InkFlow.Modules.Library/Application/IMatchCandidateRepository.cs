using InkFlow.Modules.Library.Domain;

namespace InkFlow.Modules.Library.Application;

/// <summary>匹配候选仓储契约。</summary>
public interface IMatchCandidateRepository
{
    Task AddAsync(MatchCandidate candidate, CancellationToken cancellationToken = default);

    /// <summary>查找某来源书目当前的匹配候选；不存在返回 null。</summary>
    Task<MatchCandidate?> FindForSourceBookAsync(
        string sourceId, string externalBookId, CancellationToken cancellationToken = default);
}
