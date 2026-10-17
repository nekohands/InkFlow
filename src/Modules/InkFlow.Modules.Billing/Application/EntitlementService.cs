using InkFlow.Modules.Billing.Domain;

namespace InkFlow.Modules.Billing.Application;

/// <summary>
/// 权益应用门面：保留 API/DI 入口，同时把读取与授权变更交给各自用例。
/// </summary>
public sealed class EntitlementService : IEntitlementService
{
    private readonly EntitlementQueries queries;
    private readonly EntitlementCommands commands;

    public EntitlementService(
        IPlanRepository plans,
        IEntitlementAssignmentRepository assignments,
        IBillingUserStatusReader users,
        TimeProvider clock)
    {
        queries = new(plans, assignments, users);
        commands = new(plans, assignments, users, clock);
    }

    public Task<IReadOnlyList<PlanView>> ListPlansAsync(
        CancellationToken cancellationToken = default) =>
        queries.ListPlansAsync(cancellationToken);

    public Task<EntitlementView?> GetForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default) =>
        queries.GetForUserAsync(userId, cancellationToken);

    public Task<EntitlementOperationResult> AssignAsync(
        Guid actorId,
        Guid userId,
        string? planCode,
        string? reason,
        CancellationToken cancellationToken = default) =>
        commands.AssignAsync(actorId, userId, planCode, reason, cancellationToken);
}
