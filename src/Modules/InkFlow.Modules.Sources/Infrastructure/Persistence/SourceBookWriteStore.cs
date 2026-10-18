using InkFlow.Modules.Sources.Domain;
using Microsoft.EntityFrameworkCore;

namespace InkFlow.Modules.Sources.Infrastructure.Persistence;

internal sealed class SourceBookWriteStore(SourcesDbContext db)
{
    public async Task AddAsync(SourceBook book, CancellationToken cancellationToken = default)
    {
        db.SourceBooks.Add(ToEntity(book));
        db.SourceChapters.AddRange(book.Chapters.Select(ToEntity));
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
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

    private static SourceBookEntity ToEntity(SourceBook book) => new()
    {
        Id = book.Id,
        SourceId = book.SourceId,
        ExternalBookId = book.ExternalBookId,
        Title = book.Title,
        Author = book.Author,
        CreatedAt = book.CreatedAt,
        UpdatedAt = book.UpdatedAt,
    };

    private static SourceChapterEntity ToEntity(SourceChapter chapter) => new()
    {
        Id = chapter.Id,
        SourceBookId = chapter.SourceBookId,
        ExternalChapterId = chapter.ExternalChapterId,
        ChapterIndex = chapter.Index,
        Title = chapter.Title,
    };
}
