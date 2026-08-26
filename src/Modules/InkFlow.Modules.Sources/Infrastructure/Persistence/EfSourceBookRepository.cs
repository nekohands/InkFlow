using InkFlow.Modules.Sources.Application;
using InkFlow.Modules.Sources.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace InkFlow.Modules.Sources.Infrastructure.Persistence;

public sealed class EfSourceBookRepository(SourcesDbContext db) : ISourceBookRepository
{
    public async Task AddAsync(SourceBook book, CancellationToken cancellationToken = default)
    {
        db.SourceBooks.Add(ToEntity(book));
        db.SourceChapters.AddRange(book.Chapters.Select(ToEntity));
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<SourceBook?> GetAsync(
        string sourceId, string externalBookId, CancellationToken cancellationToken = default)
    {
        var entity = await db.SourceBooks
            .SingleOrDefaultAsync(b => b.SourceId == sourceId && b.ExternalBookId == externalBookId, cancellationToken)
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

    internal static SourceBook ToDomain(SourceBookEntity entity, IEnumerable<SourceChapterEntity> chapters) =>
        SourceBook.Rehydrate(
            entity.Id,
            entity.SourceId,
            entity.ExternalBookId,
            entity.Title,
            entity.Author,
            entity.CreatedAt,
            entity.UpdatedAt,
            chapters.Select(c => new SourceChapter(c.Id, c.SourceBookId, c.ExternalChapterId, c.ChapterIndex, c.Title)));
}
