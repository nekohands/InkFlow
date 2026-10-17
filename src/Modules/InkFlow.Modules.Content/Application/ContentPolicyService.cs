using InkFlow.Modules.Content.Domain;

namespace InkFlow.Modules.Content.Application;

/// <summary>
/// 内容公开策略服务。当前采用书级策略；状态由不可变决策历史派生，命令具备同状态幂等性。
/// </summary>
public sealed class ContentPolicyService(
    IContentPolicyRepository repository,
    TimeProvider clock) : IContentPolicyService
{
    private readonly ContentPolicyReader reader = new(repository);
    private readonly ContentPolicyCommands commands = new(repository, clock);

    public const int MaxListLimit = ContentPolicyRules.MaxListLimit;

    public Task<bool> IsTakedownAsync(
        Guid canonicalBookId,
        CancellationToken cancellationToken = default)
        => reader.IsTakedownAsync(canonicalBookId, cancellationToken);

    public Task<IReadOnlySet<Guid>> ListTakedownBookIdsAsync(
        IReadOnlyCollection<Guid> canonicalBookIds,
        CancellationToken cancellationToken = default)
        => reader.ListTakedownBookIdsAsync(canonicalBookIds, cancellationToken);

    public Task<ContentPolicyCommandResult> TakedownAsync(
        Guid canonicalBookId,
        string actorId,
        string reason,
        CancellationToken cancellationToken = default) =>
        commands.TakedownAsync(canonicalBookId, actorId, reason, cancellationToken);

    public Task<ContentPolicyCommandResult> RestoreAsync(
        Guid canonicalBookId,
        string actorId,
        string reason,
        CancellationToken cancellationToken = default) =>
        commands.RestoreAsync(canonicalBookId, actorId, reason, cancellationToken);

    public Task<ContentPolicyStatus> GetStatusAsync(
        Guid canonicalBookId,
        CancellationToken cancellationToken = default)
        => reader.GetStatusAsync(canonicalBookId, cancellationToken);

    public Task<IReadOnlyList<ContentPolicyStatus>> ListAsync(
        bool takenDownOnly,
        int limit,
        CancellationToken cancellationToken = default)
        => reader.ListAsync(takenDownOnly, limit, cancellationToken);
}
