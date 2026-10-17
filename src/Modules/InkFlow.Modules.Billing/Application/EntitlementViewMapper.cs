using InkFlow.Modules.Billing.Domain;

namespace InkFlow.Modules.Billing.Application;

internal static class EntitlementViewMapper
{
    public static PlanView ToPlanView(PlanDefinition plan) =>
        new(
            plan.Code,
            plan.Version,
            plan.Name,
            plan.MonthlyQuotaUnits,
            plan.QuotaAlgorithmVersion,
            plan.Entitlements);
}
