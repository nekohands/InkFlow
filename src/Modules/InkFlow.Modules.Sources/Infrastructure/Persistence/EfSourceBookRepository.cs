using InkFlow.Modules.Sources.Application;
using InkFlow.Modules.Sources.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace InkFlow.Modules.Sources.Infrastructure.Persistence;

public sealed class EfSourceBookRepository(SourcesDbContext db) : ISourceBookRepository
{
    private readonly SourceBookQueryStore queryStore = new(db);

    public async Task AddAsync(SourceBook book, CancellationToken cancellationToken = default)
    {
        db.SourceBooks.Add(ToEntity(book));
        db.SourceChapters.AddRange(book.Chapters.Select(ToEntity));
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task<SourceBook?> GetAsync(
        string sourceId, string externalBookId, CancellationToken cancellationToken = default) =>
        queryStore.GetAsync(sourceId, externalBookId, cancellationToken);

    public Task<SourceBookMetadata?> GetMetadataAsync(
        string sourceId,
        string externalBookId,
        CancellationToken cancellationToken = default) =>
        queryStore.GetMetadataAsync(sourceId, externalBookId, cancellationToken);

    public Task<IReadOnlyList<string>> ListChapterIdsAsync(
        string sourceId,
        string externalBookId,
        CancellationToken cancellationToken = default) =>
        queryStore.ListChapterIdsAsync(sourceId, externalBookId, cancellationToken);

    public Task<SourceChapterLookup> GetChapterAsync(
        string sourceId,
        string externalBookId,
        string externalChapterId,
        CancellationToken cancellationToken = default) =>
        queryStore.GetChapterAsync(sourceId, externalBookId, externalChapterId, cancellationToken);

    public Task<IReadOnlyList<SourceBook>> ListAllAsync(CancellationToken cancellationToken = default) =>
        queryStore.ListAllAsync(cancellationToken);

    public Task<SourceBook?> FindFirstForSourceAsync(
        string sourceId,
        CancellationToken cancellationToken = default) =>
        queryStore.FindFirstForSourceAsync(sourceId, cancellationToken);

    public Task<SourceBookPage> ListPageAsync(
        SourceBookScanCursor? after,
        int limit,
        CancellationToken cancellationToken = default) =>
        queryStore.ListPageAsync(after, limit, cancellationToken);

    public async Task SaveAsync(SourceBook book, CancellationToken cancellationToken = default)
    {
        var entity = await db.SourceBooks.FindAsync([book.Id], cancellationToken).ConfigureAwait(false)
                     ?? throw new InvalidOperationException(
                         $"source book {book.Id} does not exist; use AddAsync first.");

        entity.Title = book.Title;
        entity.Author = book.Author;
        entity.UpdatedAt = book.UpdatedAt;

        // 章节幂等同步：按 ID 找出尚未持久化的新章节插入。
        var existingIds = await db.SourceChapters
            .Where(c => c.SourceBookId == book.Id)
            .Select(c => c.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var chapter in book.Chapters.Where(c => !existingIds.Contains(c.Id)))
        {
            db.SourceChapters.Add(ToEntity(chapter));
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    internal static SourceBookEntity ToEntity(SourceBook book) => new()
    {
        Id = book.Id,
        SourceId = book.SourceId,
        ExternalBookId = book.ExternalBookId,
        Title = book.Title,
        Author = book.Author,
        CreatedAt = book.CreatedAt,
        UpdatedAt = book.UpdatedAt,
    };

    internal static SourceChapterEntity ToEntity(SourceChapter chapter) => new()
    {
        Id = chapter.Id,
        SourceBookId = chapter.SourceBookId,
        ExternalChapterId = chapter.ExternalChapterId,
        ChapterIndex = chapter.Index,
        Title = chapter.Title,
    };

}
