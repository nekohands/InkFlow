using InkFlow.Modules.Billing.Domain;

namespace InkFlow.Modules.Billing.Application;

/// <summary>读取计划目录和用户当前权益，不执行授权变更。</summary>
internal sealed class EntitlementQueries(
    IPlanRepository plans,
    IEntitlementAssignmentRepository assignments,
    IBillingUserStatusReader users)
{
    public async Task<IReadOnlyList<PlanView>> ListPlansAsync(
        CancellationToken cancellationToken = default)
    {
        var definitions = await plans.ListAsync(cancellationToken).ConfigureAwait(false);
        return definitions.Select(EntitlementViewMapper.ToPlanView).ToList();
    }

    public async Task<EntitlementView?> GetForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty || !await users.IsActiveAsync(userId, cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        var assignment = await assignments.GetLatestForUserAsync(userId, cancellationToken)
            .ConfigureAwait(false);
        var plan = assignment is null
            ? await plans.GetAsync(
                    CommercialPlanCodes.Free,
                    CommercialPlanCodes.Version,
                    cancellationToken)
                .ConfigureAwait(false)
            : await plans.GetAsync(assignment.PlanCode, assignment.PlanVersion, cancellationToken)
                .ConfigureAwait(false);
        if (plan is null)
        {
            return null;
        }

        return new EntitlementView(
            userId,
            EntitlementViewMapper.ToPlanView(plan),
            assignment?.CreatedAt ?? DateTimeOffset.MinValue);
    }
}
