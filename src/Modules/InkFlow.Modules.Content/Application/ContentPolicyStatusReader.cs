using InkFlow.Modules.Content.Domain;

namespace InkFlow.Modules.Content.Application;

/// <summary>
/// 管理端策略状态与有界列表投影读取边界。
/// </summary>
internal sealed class ContentPolicyStatusReader(IContentPolicyRepository repository)
{
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
            latest?.IsTakedown == true,
            latest);
}
