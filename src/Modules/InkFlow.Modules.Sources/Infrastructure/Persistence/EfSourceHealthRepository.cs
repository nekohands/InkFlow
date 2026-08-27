using InkFlow.Modules.Sources.Application;
using InkFlow.Modules.Sources.Domain;
using Microsoft.EntityFrameworkCore;

namespace InkFlow.Modules.Sources.Infrastructure.Persistence;

public sealed class EfSourceHealthRepository(SourcesDbContext db) : ISourceHealthRepository
{
    public async Task<SourceCapabilityHealth?> GetAsync(
        string sourceId,
        SourceCapability capability,
        CancellationToken cancellationToken = default)
    {
        var entity = await db.CapabilityHealth
            .FindAsync([sourceId, capability], cancellationToken)
            .ConfigureAwait(false);

        return entity is null ? null : ToDomain(entity);
    }

    public async Task AddAsync(
        SourceCapabilityHealth health,
        CancellationToken cancellationToken = default)
    {
        db.CapabilityHealth.Add(ToEntity(health));
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task SaveAsync(
        SourceCapabilityHealth health,
        CancellationToken cancellationToken = default)
    {
        var entity = await db.CapabilityHealth
            .FindAsync([health.SourceId, health.Capability], cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                $"source capability health '{health.SourceId}/{health.Capability}' does not exist; use AddAsync first.");

        ApplyDomain(health, entity);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<SourceCapabilityHealth>> ListForSourceAsync(
        string sourceId,
        CancellationToken cancellationToken = default)
    {
        var entities = await db.CapabilityHealth
            .Where(x => x.SourceId == sourceId)
            .OrderBy(x => x.Capability)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(ToDomain).ToList();
    }

    private static SourceCapabilityHealthEntity ToEntity(SourceCapabilityHealth health) => new()
    {
        SourceId = health.SourceId,
        Capability = health.Capability,
        Status = health.Status,
        ConsecutiveFailures = health.ConsecutiveFailures,
        LastSuccessAt = health.LastSuccessAt,
        LastFailureAt = health.LastFailureAt,
        LastFailureReason = health.LastFailureReason,
        AlgorithmVersion = health.AlgorithmVersion,
        UpdatedAt = health.UpdatedAt,
    };

    private static SourceCapabilityHealth ToDomain(SourceCapabilityHealthEntity entity) =>
        SourceCapabilityHealth.Rehydrate(
            entity.SourceId,
            entity.Capability,
            entity.Status,
            entity.ConsecutiveFailures,
            entity.LastSuccessAt,
            entity.LastFailureAt,
            entity.LastFailureReason,
            entity.AlgorithmVersion,
            entity.UpdatedAt);

    private static void ApplyDomain(
        SourceCapabilityHealth health,
        SourceCapabilityHealthEntity entity)
    {
        entity.Status = health.Status;
        entity.ConsecutiveFailures = health.ConsecutiveFailures;
        entity.LastSuccessAt = health.LastSuccessAt;
        entity.LastFailureAt = health.LastFailureAt;
        entity.LastFailureReason = health.LastFailureReason;
        entity.AlgorithmVersion = health.AlgorithmVersion;
        entity.UpdatedAt = health.UpdatedAt;
    }
}
