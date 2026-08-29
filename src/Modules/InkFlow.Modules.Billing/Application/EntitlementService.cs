using InkFlow.Modules.Billing.Domain;

namespace InkFlow.Modules.Billing.Application;

public sealed class EntitlementService(
    IPlanRepository plans,
    IEntitlementAssignmentRepository assignments,
    IBillingUserStatusReader users,
    TimeProvider clock) : IEntitlementService
{
    public async Task<IReadOnlyList<PlanView>> ListPlansAsync(
        CancellationToken cancellationToken = default)
    {
        var definitions = await plans.ListAsync(cancellationToken).ConfigureAwait(false);
        return definitions.Select(ToView).ToList();
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
            ToView(plan),
            assignment?.CreatedAt ?? DateTimeOffset.MinValue);
    }

    public async Task<EntitlementOperationResult> AssignAsync(
        Guid actorId,
        Guid userId,
        string? planCode,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        if (actorId == Guid.Empty || userId == Guid.Empty || string.IsNullOrWhiteSpace(planCode) ||
            string.IsNullOrWhiteSpace(reason))
        {
            return new EntitlementOperationResult(EntitlementOperationStatus.InvalidRequest);
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
                new EntitlementView(userId, ToView(plan), assignment.CreatedAt));
        }
        catch (ArgumentException)
        {
            return new EntitlementOperationResult(EntitlementOperationStatus.InvalidRequest);
        }
    }

    private static PlanView ToView(PlanDefinition plan) =>
        new(
            plan.Code,
            plan.Version,
            plan.Name,
            plan.MonthlyQuotaUnits,
            plan.QuotaAlgorithmVersion,
            plan.Entitlements);
}
