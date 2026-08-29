using System.Text.Json;
using InkFlow.Modules.Billing.Application;
using InkFlow.Modules.Billing.Domain;
using Microsoft.EntityFrameworkCore;

namespace InkFlow.Modules.Billing.Infrastructure.Persistence;

public static class BillingMapper
{
    public static PlanDefinition ToDomain(PlanEntity entity) =>
        PlanDefinition.Rehydrate(
            entity.Code,
            entity.Version,
            entity.Name,
            entity.MonthlyQuotaUnits,
            entity.QuotaAlgorithmVersion,
            JsonSerializer.Deserialize<string[]>(entity.EntitlementsJson) ?? []);

    public static EntitlementAssignmentEntity ToEntity(EntitlementAssignment assignment) => new()
    {
        Id = assignment.Id,
        UserId = assignment.UserId,
        PlanCode = assignment.PlanCode,
        PlanVersion = assignment.PlanVersion,
        AssignedBy = assignment.AssignedBy,
        Reason = assignment.Reason,
        CreatedAt = assignment.CreatedAt,
    };

    public static EntitlementAssignment ToDomain(EntitlementAssignmentEntity entity) =>
        EntitlementAssignment.Rehydrate(
            entity.Id,
            entity.UserId,
            entity.PlanCode,
            entity.PlanVersion,
            entity.AssignedBy,
            entity.Reason,
            entity.CreatedAt);

    public static UsageLedgerEntity ToEntity(UsageLedgerEntry entry) => new()
    {
        Id = entry.Id,
        UserId = entry.UserId,
        ApplicationId = entry.ApplicationId,
        ApiKeyId = entry.ApiKeyId,
        PeriodStart = entry.PeriodStart,
        Operation = entry.Operation,
        Units = entry.Units,
        AlgorithmVersion = entry.AlgorithmVersion,
        OccurredAt = entry.OccurredAt,
        TraceId = entry.TraceId,
    };
}

public sealed class EfPlanRepository(BillingDbContext db) : IPlanRepository
{
    public async Task<IReadOnlyList<PlanDefinition>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        var entities = await db.Plans
            .AsNoTracking()
            .OrderBy(x => x.Code)
            .ThenBy(x => x.Version)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return entities.Count == 0
            ? BuiltInPlans.All
            : entities.Select(BillingMapper.ToDomain).ToList();
    }

    public async Task<PlanDefinition?> GetAsync(
        string code,
        int version,
        CancellationToken cancellationToken = default)
    {
        var entity = await db.Plans
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.Code == code && x.Version == version,
                cancellationToken)
            .ConfigureAwait(false);
        return entity is null ? null : BillingMapper.ToDomain(entity);
    }
}

public sealed class EfEntitlementAssignmentRepository(BillingDbContext db)
    : IEntitlementAssignmentRepository
{
    public async Task<EntitlementAssignment?> GetLatestForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var entity = await db.EntitlementAssignments
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        return entity is null ? null : BillingMapper.ToDomain(entity);
    }

    public async Task AddAsync(
        EntitlementAssignment assignment,
        CancellationToken cancellationToken = default)
    {
        db.EntitlementAssignments.Add(BillingMapper.ToEntity(assignment));
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
