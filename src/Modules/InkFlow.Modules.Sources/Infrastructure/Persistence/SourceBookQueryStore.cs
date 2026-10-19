using InkFlow.Modules.Sources.Application;
using InkFlow.Modules.Sources.Domain;
using Microsoft.EntityFrameworkCore;

namespace InkFlow.Modules.Sources.Infrastructure.Persistence;

internal sealed class SourceBookQueryStore(SourcesDbContext db)
{
    public async Task<SourceBook?> GetAsync(
        string sourceId, string externalBookId, CancellationToken cancellationToken = default)
    {
        var entity = await BookQuery(sourceId, externalBookId)
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (entity is null)
        {
            return null;
        }

        var chapters = await db.SourceChapters
            .Where(c => c.SourceBookId == entity.Id)
            .OrderBy(c => c.ChapterIndex)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return ToDomain(entity, chapters);
    }

    public async Task<SourceBookMetadata?> GetMetadataAsync(
        string sourceId,
        string externalBookId,
        CancellationToken cancellationToken = default)
    {
        return await BookQuery(sourceId, externalBookId)
            .AsNoTracking()
            .Select(book => new SourceBookMetadata(book.Title, book.Author))
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<string>> ListChapterIdsAsync(
        string sourceId,
        string externalBookId,
        CancellationToken cancellationToken = default)
    {
        return await (
                from book in BookQuery(sourceId, externalBookId).AsNoTracking()
                join chapter in db.SourceChapters.AsNoTracking()
                    on book.Id equals chapter.SourceBookId
                orderby chapter.ChapterIndex
                select chapter.ExternalChapterId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<SourceChapterLookup> GetChapterAsync(
        string sourceId,
        string externalBookId,
        string externalChapterId,
        CancellationToken cancellationToken = default)
    {
        var result = await (
            from book in BookQuery(sourceId, externalBookId).AsNoTracking()
            join chapter in db.SourceChapters.AsNoTracking()
                    .Where(chapter => chapter.ExternalChapterId == externalChapterId)
                on book.Id equals chapter.SourceBookId into matchingChapters
            from chapter in matchingChapters.DefaultIfEmpty()
            select new { BookId = book.Id, Chapter = chapter })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return result is null
            ? new SourceChapterLookup(false, null)
            : new SourceChapterLookup(
                true,
                result.Chapter is null ? null : ToDomain(result.Chapter));
    }

    public async Task<IReadOnlyList<SourceBook>> ListAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await db.SourceBooks
            .OrderBy(b => b.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities
            .Select(e => SourceBook.Rehydrate(
                e.Id, e.SourceId, e.ExternalBookId, e.Title, e.Author,
                e.CreatedAt, e.UpdatedAt, []))
            .ToList();
    }

    public async Task<SourceBook?> FindFirstForSourceAsync(
        string sourceId,
        CancellationToken cancellationToken = default)
    {
        var entity = await db.SourceBooks
            .AsNoTracking()
            .Where(book => book.SourceId == sourceId)
            .OrderBy(book => book.CreatedAt)
            .ThenBy(book => book.Id)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return entity is null
            ? null
            : SourceBook.Rehydrate(
                entity.Id,
                entity.SourceId,
                entity.ExternalBookId,
                entity.Title,
                entity.Author,
                entity.CreatedAt,
                entity.UpdatedAt,
                []);
    }

    public async Task<SourceBookPage> ListPageAsync(
        SourceBookScanCursor? after,
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > 1000)
        {
            throw new ArgumentOutOfRangeException(nameof(limit));
        }

        var query = db.SourceBooks.AsNoTracking();
        if (after is not null)
        {
            query = query.Where(b =>
                b.CreatedAt > after.CreatedAt ||
                (b.CreatedAt == after.CreatedAt && b.Id.CompareTo(after.Id) > 0));
        }

        var entities = await query
            .OrderBy(b => b.CreatedAt)
            .ThenBy(b => b.Id)
            .Take(limit + 1)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var hasMore = entities.Count > limit;
        if (hasMore)
        {
            entities.RemoveAt(limit);
        }

        var books = entities
            .Select(e => SourceBook.Rehydrate(
                e.Id, e.SourceId, e.ExternalBookId, e.Title, e.Author,
                e.CreatedAt, e.UpdatedAt, []))
            .ToList();
        var nextCursor = hasMore
            ? new SourceBookScanCursor(entities[^1].CreatedAt, entities[^1].Id)
            : null;
        return new SourceBookPage(books, nextCursor, hasMore);
    }

    private IQueryable<SourceBookEntity> BookQuery(
        string sourceId,
        string externalBookId) =>
        db.SourceBooks.Where(book =>
            book.SourceId == sourceId && book.ExternalBookId == externalBookId);

    private static SourceBook ToDomain(
        SourceBookEntity entity,
        IEnumerable<SourceChapterEntity> chapters) =>
        SourceBook.Rehydrate(
            entity.Id,
            entity.SourceId,
            entity.ExternalBookId,
            entity.Title,
            entity.Author,
            entity.CreatedAt,
            entity.UpdatedAt,
            chapters.Select(ToDomain));

    private static SourceChapter ToDomain(SourceChapterEntity entity) =>
        new(entity.Id, entity.SourceBookId, entity.ExternalChapterId, entity.ChapterIndex, entity.Title);
}
