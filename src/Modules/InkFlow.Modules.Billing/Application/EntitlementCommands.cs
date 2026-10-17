using InkFlow.Modules.Billing.Domain;

namespace InkFlow.Modules.Billing.Application;

/// <summary>校验管理员并追加用户权益授权的命令用例。</summary>
internal sealed class EntitlementCommands(
    IPlanRepository plans,
    IEntitlementAssignmentRepository assignments,
    IBillingUserStatusReader users,
    TimeProvider clock)
{
    public async Task<EntitlementOperationResult> AssignAsync(
        Guid actorId,
        Guid userId,
        string? planCode,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        if (actorId == Guid.Empty)
        {
            return new EntitlementOperationResult(EntitlementOperationStatus.ActorNotAllowed);
        }

        if (userId == Guid.Empty || string.IsNullOrWhiteSpace(planCode) ||
            string.IsNullOrWhiteSpace(reason))
        {
            return new EntitlementOperationResult(EntitlementOperationStatus.InvalidRequest);
        }

        if (!await users.IsActiveAdministratorAsync(actorId, cancellationToken).ConfigureAwait(false))
        {
            return new EntitlementOperationResult(EntitlementOperationStatus.ActorNotAllowed);
        }

        if (!await users.IsActiveAsync(userId, cancellationToken).ConfigureAwait(false))
        {
            return new EntitlementOperationResult(EntitlementOperationStatus.UserNotFound);
        }

        var plan = await plans.GetAsync(
                planCode.Trim().ToLowerInvariant(),
                CommercialPlanCodes.Version,
                cancellationToken)
            .ConfigureAwait(false);
        if (plan is null)
        {
            return new EntitlementOperationResult(EntitlementOperationStatus.PlanNotFound);
        }

        try
        {
            var assignment = EntitlementAssignment.Create(
                userId,
                plan.Code,
                plan.Version,
                actorId,
                reason,
                clock.GetUtcNow());
            await assignments.AddAsync(assignment, cancellationToken).ConfigureAwait(false);
            return new EntitlementOperationResult(
                EntitlementOperationStatus.Success,
                new EntitlementView(userId, EntitlementViewMapper.ToPlanView(plan), assignment.CreatedAt));
        }
        catch (ArgumentException)
        {
            return new EntitlementOperationResult(EntitlementOperationStatus.InvalidRequest);
        }
    }
}
