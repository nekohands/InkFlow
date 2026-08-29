using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using InkFlow.Modules.Sources.Application;
using InkFlow.Modules.Sources.Domain;
using Microsoft.EntityFrameworkCore;

namespace InkFlow.Modules.Sources.Infrastructure.Persistence;

/// <summary>
/// PostgreSQL transaction-scoped locks serialize mutations for one stable
/// (source, capability) health key across API and worker instances.
/// </summary>
internal static class SourceHealthAdvisoryLock
{
    private const int NamespaceKey = 1201;

    public static Task AcquireAsync(
        SourcesDbContext db,
        string sourceId,
        SourceCapability capability,
        CancellationToken cancellationToken)
    {
        var sourceBytes = Encoding.UTF8.GetBytes(sourceId);
        var identity = new byte[sourceBytes.Length + sizeof(int)];
        sourceBytes.AsSpan().CopyTo(identity);
        BinaryPrimitives.WriteInt32LittleEndian(
            identity.AsSpan(sourceBytes.Length),
            (int)capability);
        var hash = BinaryPrimitives.ReadInt32LittleEndian(SHA256.HashData(identity));

        return db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock({NamespaceKey}, {hash})",
            cancellationToken);
    }
}

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

    public async Task<SourceCapabilityHealth> MutateAsync(
        string sourceId,
        SourceCapability capability,
        SourceHealthMutationKind mutation,
        string? reason,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);
        await SourceHealthAdvisoryLock
            .AcquireAsync(db, sourceId, capability, cancellationToken)
            .ConfigureAwait(false);

        var entity = await db.CapabilityHealth
            .SingleOrDefaultAsync(
                x => x.SourceId == sourceId && x.Capability == capability,
                cancellationToken)
            .ConfigureAwait(false);

        var health = entity is null
            ? SourceCapabilityHealth.Create(sourceId, capability, occurredAt)
            : ToDomain(entity);
        ApplyMutation(health, mutation, reason, occurredAt);

        if (entity is null)
        {
            db.CapabilityHealth.Add(ToEntity(health));
        }
        else
        {
            ApplyDomain(health, entity);
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return health;
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

    public async Task<IReadOnlyList<SourceCapabilityHealth>> ListUnhealthyAsync(
        CancellationToken cancellationToken = default)
    {
        var entities = await db.CapabilityHealth
            .Where(x => x.Status == SourceHealthStatus.Unhealthy)
            .OrderBy(x => x.SourceId)
            .ThenBy(x => x.Capability)
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

    private static void ApplyMutation(
        SourceCapabilityHealth health,
        SourceHealthMutationKind mutation,
        string? reason,
        DateTimeOffset occurredAt)
    {
        switch (mutation)
        {
            case SourceHealthMutationKind.RecordSuccess:
                health.RecordSuccess(occurredAt);
                break;
            case SourceHealthMutationKind.RecordFailure:
                health.RecordFailure(reason ?? string.Empty, occurredAt);
                break;
            case SourceHealthMutationKind.Disable:
                health.Disable(reason ?? string.Empty, occurredAt);
                break;
            case SourceHealthMutationKind.Enable:
                health.Enable(occurredAt);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation), mutation, null);
        }
    }
}
