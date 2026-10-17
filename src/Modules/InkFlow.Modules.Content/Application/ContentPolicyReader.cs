using InkFlow.Modules.Content.Domain;

namespace InkFlow.Modules.Content.Application;

internal sealed class ContentPolicyReader(IContentPolicyRepository repository) : IContentPolicyReader
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

    public async Task<ContentPolicyStatus> GetStatusAsync(
        Guid canonicalBookId,
        CancellationToken cancellationToken = default)
    {
        ContentPolicyRules.EnsureBookId(canonicalBookId);
        var latest = await repository
            .GetLatestAsync(canonicalBookId, cancellationToken)
            .ConfigureAwait(false);

        return ToStatus(canonicalBookId, latest);
    }

    public async Task<IReadOnlyList<ContentPolicyStatus>> ListAsync(
        bool takenDownOnly,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var boundedLimit = Math.Clamp(limit, 1, ContentPolicyRules.MaxListLimit);
        var latest = await repository
            .ListLatestAsync(takenDownOnly, boundedLimit, cancellationToken)
            .ConfigureAwait(false);

        return latest
            .Select(decision => ToStatus(decision.CanonicalBookId, decision))
            .ToList();
    }

    private static ContentPolicyStatus ToStatus(
        Guid canonicalBookId,
        ContentPolicyDecision? latest) =>
        new(
            canonicalBookId,
            latest?.Action == ContentPolicyAction.Takedown,
            latest);
}
