using InkFlow.Modules.Sources.Domain;
using Microsoft.EntityFrameworkCore;

namespace InkFlow.Modules.Sources.Infrastructure.Persistence;

/// <summary>Owns read-only fetch-artifact queries used by content-fetch scheduling.</summary>
internal sealed class FetchArtifactQueryStore(SourcesDbContext db)
{
    public async Task<FetchArtifact?> GetLatestAsync(
        string sourceId,
        string externalChapterId,
        CancellationToken cancellationToken = default)
    {
        var entity = await db.FetchArtifacts
            .Where(a => a.SourceId == sourceId && a.ExternalChapterId == externalChapterId)
            .OrderByDescending(a => a.FetchedAt)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return entity is null
            ? null
            : new FetchArtifact(
                entity.Id, entity.SourceId, entity.ExternalBookId, entity.ExternalChapterId,
                entity.RawHash, entity.BodyLength, entity.FetchedAt);
    }

    public async Task<IReadOnlySet<string>> ListFetchedExternalChapterIdsAsync(
        string sourceId,
        IEnumerable<string> externalChapterIds,
        CancellationToken cancellationToken = default)
    {
        var ids = externalChapterIds.ToArray();
        if (ids.Length == 0)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        var fetched = await FetchedArtifactsQuery(sourceId, ids)
            .Select(a => a.ExternalChapterId)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new HashSet<string>(fetched, StringComparer.Ordinal);
    }

    public async Task<IReadOnlySet<string>> ListRecentlyFetchedExternalChapterIdsAsync(
        string sourceId,
        IEnumerable<string> externalChapterIds,
        DateTimeOffset since,
        CancellationToken cancellationToken = default)
    {
        var ids = externalChapterIds.ToArray();
        if (ids.Length == 0)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        var recent = await FetchedArtifactsQuery(sourceId, ids)
            .Where(a => a.FetchedAt >= since)
            .Select(a => a.ExternalChapterId)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new HashSet<string>(recent, StringComparer.Ordinal);
    }

    private IQueryable<FetchArtifactEntity> FetchedArtifactsQuery(
        string sourceId,
        IReadOnlyCollection<string> externalChapterIds) =>
        db.FetchArtifacts.Where(a =>
            a.SourceId == sourceId && externalChapterIds.Contains(a.ExternalChapterId));
}
