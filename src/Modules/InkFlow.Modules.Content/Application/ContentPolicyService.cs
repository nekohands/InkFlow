using InkFlow.Modules.Content.Domain;

namespace InkFlow.Modules.Content.Application;

/// <summary>
/// 内容公开策略服务。当前采用书级策略；状态由不可变决策历史派生，命令具备同状态幂等性。
/// </summary>
public sealed class ContentPolicyService(
    IContentPolicyRepository repository,
    TimeProvider clock) : IContentPolicyService
{
    public const int MaxListLimit = 100;

    public async Task<bool> IsTakedownAsync(
        Guid canonicalBookId,
        CancellationToken cancellationToken = default)
    {
        var latest = await repository
            .GetLatestAsync(canonicalBookId, cancellationToken)
            .ConfigureAwait(false);

        return latest?.Action == ContentPolicyAction.Takedown;
    }

    public async Task<ContentPolicyCommandResult> TakedownAsync(
        Guid canonicalBookId,
        string actorId,
        string reason,
        CancellationToken cancellationToken = default) =>
        await ApplyAsync(
            canonicalBookId,
            ContentPolicyAction.Takedown,
            actorId,
            reason,
            cancellationToken).ConfigureAwait(false);

    public async Task<ContentPolicyCommandResult> RestoreAsync(
        Guid canonicalBookId,
        string actorId,
        string reason,
        CancellationToken cancellationToken = default) =>
        await ApplyAsync(
            canonicalBookId,
            ContentPolicyAction.Restore,
            actorId,
            reason,
            cancellationToken).ConfigureAwait(false);

    public async Task<ContentPolicyStatus> GetStatusAsync(
        Guid canonicalBookId,
        CancellationToken cancellationToken = default)
    {
        EnsureBookId(canonicalBookId);
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
        var boundedLimit = Math.Clamp(limit, 1, MaxListLimit);
        var latest = await repository
            .ListLatestAsync(takenDownOnly, boundedLimit, cancellationToken)
            .ConfigureAwait(false);

        return latest
            .Select(decision => ToStatus(decision.CanonicalBookId, decision))
            .ToList();
    }

    private async Task<ContentPolicyCommandResult> ApplyAsync(
        Guid canonicalBookId,
        ContentPolicyAction action,
        string actorId,
        string reason,
        CancellationToken cancellationToken)
    {
        EnsureBookId(canonicalBookId);

        // 即使命令最终是幂等 no-op，也必须先验证操作者和理由，避免高风险命令绕过审计前置条件。
        var candidate = ContentPolicyDecision.Create(
            canonicalBookId,
            action,
            actorId,
            reason,
            clock.GetUtcNow());

        var latest = await repository
            .GetLatestAsync(canonicalBookId, cancellationToken)
            .ConfigureAwait(false);

        if (latest?.Action == action)
        {
            return new ContentPolicyCommandResult(
                canonicalBookId,
                action == ContentPolicyAction.Takedown,
                Changed: false,
                latest);
        }

        // Restore 未曾下架时是无状态幂等操作，不制造虚假的恢复历史。
        if (action == ContentPolicyAction.Restore && latest is null)
        {
            return new ContentPolicyCommandResult(
                canonicalBookId,
                IsTakedown: false,
                Changed: false,
                Decision: null);
        }

        await repository.AddAsync(candidate, cancellationToken).ConfigureAwait(false);

        return new ContentPolicyCommandResult(
            canonicalBookId,
            action == ContentPolicyAction.Takedown,
            Changed: true,
            candidate);
    }

    private static ContentPolicyStatus ToStatus(
        Guid canonicalBookId,
        ContentPolicyDecision? latest) =>
        new(
            canonicalBookId,
            latest?.Action == ContentPolicyAction.Takedown,
            latest);

    private static void EnsureBookId(Guid canonicalBookId)
    {
        if (canonicalBookId == Guid.Empty)
        {
            throw new ArgumentException("canonicalBookId must not be empty.", nameof(canonicalBookId));
        }
    }
}
