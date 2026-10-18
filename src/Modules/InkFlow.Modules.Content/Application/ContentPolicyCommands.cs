using InkFlow.Modules.Content.Domain;

namespace InkFlow.Modules.Content.Application;

internal sealed class ContentPolicyCommands(
    IContentPolicyRepository repository,
    TimeProvider clock)
{
    public Task<ContentPolicyCommandResult> TakedownAsync(
        Guid canonicalBookId,
        string actorId,
        string reason,
        CancellationToken cancellationToken = default) =>
        ApplyAsync(
            canonicalBookId,
            ContentPolicyAction.Takedown,
            actorId,
            reason,
            cancellationToken);

    public Task<ContentPolicyCommandResult> RestoreAsync(
        Guid canonicalBookId,
        string actorId,
        string reason,
        CancellationToken cancellationToken = default) =>
        ApplyAsync(
            canonicalBookId,
            ContentPolicyAction.Restore,
            actorId,
            reason,
            cancellationToken);

    private async Task<ContentPolicyCommandResult> ApplyAsync(
        Guid canonicalBookId,
        ContentPolicyAction action,
        string actorId,
        string reason,
        CancellationToken cancellationToken)
    {
        ContentPolicyRules.EnsureBookId(canonicalBookId);

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
                latest.IsTakedown,
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
            candidate.IsTakedown,
            Changed: true,
            candidate);
    }
}
