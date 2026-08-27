using InkFlow.Modules.Content.Domain;

namespace InkFlow.Modules.Content.Application;

/// <summary>公开读取路径使用的最小策略查询端口。</summary>
public interface IContentPolicyReader
{
    Task<bool> IsTakedownAsync(
        Guid canonicalBookId,
        CancellationToken cancellationToken = default);
}

/// <summary>政策决策的追加式持久化端口。</summary>
public interface IContentPolicyRepository
{
    Task<ContentPolicyDecision?> GetLatestAsync(
        Guid canonicalBookId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ContentPolicyDecision>> ListLatestAsync(
        bool takenDownOnly,
        int limit,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        ContentPolicyDecision decision,
        CancellationToken cancellationToken = default);
}

public sealed record ContentPolicyStatus(
    Guid CanonicalBookId,
    bool IsTakedown,
    ContentPolicyDecision? LatestDecision);

public sealed record ContentPolicyCommandResult(
    Guid CanonicalBookId,
    bool IsTakedown,
    bool Changed,
    ContentPolicyDecision? Decision);

public interface IContentPolicyService : IContentPolicyReader
{
    Task<ContentPolicyCommandResult> TakedownAsync(
        Guid canonicalBookId,
        string actorId,
        string reason,
        CancellationToken cancellationToken = default);

    Task<ContentPolicyCommandResult> RestoreAsync(
        Guid canonicalBookId,
        string actorId,
        string reason,
        CancellationToken cancellationToken = default);

    Task<ContentPolicyStatus> GetStatusAsync(
        Guid canonicalBookId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ContentPolicyStatus>> ListAsync(
        bool takenDownOnly,
        int limit,
        CancellationToken cancellationToken = default);
}
