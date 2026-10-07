using InkFlow.Modules.Content.Domain;

namespace InkFlow.Modules.Content.Application;

/// <summary>公开读取路径使用的最小策略查询端口。</summary>
public interface IContentPolicyReader
{
    Task<bool> IsTakedownAsync(
        Guid canonicalBookId,
        CancellationToken cancellationToken = default);

    async Task<IReadOnlySet<Guid>> ListTakedownBookIdsAsync(
        IReadOnlyCollection<Guid> canonicalBookIds,
        CancellationToken cancellationToken = default)
    {
        var takenDown = new HashSet<Guid>();
        foreach (var bookId in canonicalBookIds.Where(id => id != Guid.Empty).Distinct())
        {
            if (await IsTakedownAsync(bookId, cancellationToken).ConfigureAwait(false))
            {
                takenDown.Add(bookId);
            }
        }

        return takenDown;
    }
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

    async Task<IReadOnlyList<ContentPolicyDecision>> ListLatestForBooksAsync(
        IReadOnlyCollection<Guid> canonicalBookIds,
        CancellationToken cancellationToken = default)
    {
        var latest = new List<ContentPolicyDecision>();
        foreach (var bookId in canonicalBookIds.Where(id => id != Guid.Empty).Distinct())
        {
            var decision = await GetLatestAsync(bookId, cancellationToken).ConfigureAwait(false);
            if (decision is not null)
            {
                latest.Add(decision);
            }
        }

        return latest;
    }

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
