using InkFlow.Modules.Content.Domain;

namespace InkFlow.Modules.Content.Application;

/// <summary>
/// 公开内容路径使用的策略可见性读取边界。
/// </summary>
internal sealed class ContentPolicyVisibilityReader(IContentPolicyRepository repository)
    : IContentPolicyReader
{
    public async Task<bool> IsTakedownAsync(
        Guid canonicalBookId,
        CancellationToken cancellationToken = default)
    {
        var latest = await repository
            .GetLatestAsync(canonicalBookId, cancellationToken)
            .ConfigureAwait(false);

        return latest?.Action == ContentPolicyAction.Takedown;
    }

    public async Task<IReadOnlySet<Guid>> ListTakedownBookIdsAsync(
        IReadOnlyCollection<Guid> canonicalBookIds,
        CancellationToken cancellationToken = default)
    {
        var safeIds = canonicalBookIds
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToArray();
        if (safeIds.Length == 0)
        {
            return new HashSet<Guid>();
        }

        var latest = await repository
            .ListLatestForBooksAsync(safeIds, cancellationToken)
            .ConfigureAwait(false);

        return latest
            .Where(decision => decision.Action == ContentPolicyAction.Takedown)
            .Select(decision => decision.CanonicalBookId)
            .ToHashSet();
    }
}
