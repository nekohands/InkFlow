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

internal sealed class SourceHealthMutationStore(SourcesDbContext db)
{
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
            : SourceHealthMapper.ToDomain(entity);
        ApplyMutation(health, mutation, reason, occurredAt);

        if (entity is null)
        {
            db.CapabilityHealth.Add(SourceHealthMapper.ToEntity(health));
        }
        else
        {
            SourceHealthMapper.ApplyDomain(health, entity);
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return health;
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
