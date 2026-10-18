using InkFlow.Modules.Sources.Application;
using InkFlow.Modules.Sources.Domain;
using Microsoft.EntityFrameworkCore;

namespace InkFlow.Modules.Sources.Infrastructure.Persistence;

public sealed class EfSourceHealthRepository(SourcesDbContext db) : ISourceHealthRepository
{
    private readonly SourceHealthQueryStore queryStore = new(db);
    private readonly SourceHealthMutationStore mutationStore = new(db);

    public Task<SourceCapabilityHealth?> GetAsync(
        string sourceId,
        SourceCapability capability,
        CancellationToken cancellationToken = default) =>
        queryStore.GetAsync(sourceId, capability, cancellationToken);

    public async Task AddAsync(
        SourceCapabilityHealth health,
        CancellationToken cancellationToken = default)
    {
        db.CapabilityHealth.Add(SourceHealthMapper.ToEntity(health));
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

        SourceHealthMapper.ApplyDomain(health, entity);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task<SourceCapabilityHealth> MutateAsync(
        string sourceId,
        SourceCapability capability,
        SourceHealthMutationKind mutation,
        string? reason,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken = default) =>
        mutationStore.MutateAsync(
            sourceId, capability, mutation, reason, occurredAt, cancellationToken);

    public Task<IReadOnlyList<SourceCapabilityHealth>> ListForSourceAsync(
        string sourceId,
        CancellationToken cancellationToken = default) =>
        queryStore.ListForSourceAsync(sourceId, cancellationToken);

    public Task<IReadOnlyList<SourceCapabilityHealth>> ListUnhealthyAsync(
        CancellationToken cancellationToken = default) =>
        queryStore.ListUnhealthyAsync(cancellationToken);

    public Task<SourceHealthPage> ListUnhealthyPageAsync(
        SourceHealthScanCursor? after,
        int limit,
        CancellationToken cancellationToken = default) =>
        queryStore.ListUnhealthyPageAsync(after, limit, cancellationToken);

}
