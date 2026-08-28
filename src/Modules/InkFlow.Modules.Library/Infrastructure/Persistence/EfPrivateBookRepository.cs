using InkFlow.Modules.Library.Application;
using InkFlow.Modules.Library.Domain;
using Microsoft.EntityFrameworkCore;

namespace InkFlow.Modules.Library.Infrastructure.Persistence;

public sealed class EfPrivateBookRepository(LibraryDbContext db) : IPrivateBookRepository
{
    public async Task AddAsync(PrivateBook book, CancellationToken cancellationToken = default)
    {
        db.PrivateBooks.Add(ToEntity(book));
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task AddWithChaptersAsync(
        PrivateBook book,
        IReadOnlyCollection<PrivateChapter> chapters,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);
        db.PrivateBooks.Add(ToEntity(book));
        db.PrivateChapters.AddRange(chapters.Select(ToEntity));
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<PrivateBook?> GetAsync(
        Guid userId,
        Guid privateBookId,
        CancellationToken cancellationToken = default)
    {
        var entity = await db.PrivateBooks
            .AsNoTracking()
            .SingleOrDefaultAsync(
                book => book.UserId == userId && book.Id == privateBookId,
                cancellationToken)
            .ConfigureAwait(false);

        return entity is null ? null : ToDomain(entity);
    }

    public async Task<IReadOnlyList<PrivateBook>> ListAsync(
        Guid userId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var entities = await db.PrivateBooks
            .AsNoTracking()
            .Where(book => book.UserId == userId)
            .OrderByDescending(book => book.CreatedAt)
            .ThenByDescending(book => book.Id)
            .Take(limit)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(ToDomain).ToList();
    }

    public async Task<bool> SaveAsync(
        PrivateBook book,
        CancellationToken cancellationToken = default)
    {
        var entity = await db.PrivateBooks
            .SingleOrDefaultAsync(
                candidate => candidate.UserId == book.UserId && candidate.Id == book.Id,
                cancellationToken)
            .ConfigureAwait(false);
        if (entity is null)
        {
            return false;
        }

        entity.Title = book.Title;
        entity.Author = book.Author;
        entity.UpdatedAt = book.UpdatedAt;
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async Task<bool> DeleteAsync(
        Guid userId,
        Guid privateBookId,
        CancellationToken cancellationToken = default)
    {
        var entity = await db.PrivateBooks
            .SingleOrDefaultAsync(
                candidate => candidate.UserId == userId && candidate.Id == privateBookId,
                cancellationToken)
            .ConfigureAwait(false);
        if (entity is null)
        {
            return false;
        }

        db.PrivateBooks.Remove(entity);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async Task<IReadOnlyList<PrivateChapter>> ListChaptersAsync(
        Guid userId,
        Guid privateBookId,
        CancellationToken cancellationToken = default)
    {
        var entities = await db.PrivateChapters
            .AsNoTracking()
            .Where(chapter =>
                chapter.UserId == userId && chapter.PrivateBookId == privateBookId)
            .OrderBy(chapter => chapter.ChapterIndex)
            .ThenBy(chapter => chapter.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(ToDomain).ToList();
    }

    public async Task<PrivateChapter?> GetChapterAsync(
        Guid userId,
        Guid privateBookId,
        Guid privateChapterId,
        CancellationToken cancellationToken = default)
    {
        var entity = await db.PrivateChapters
            .AsNoTracking()
            .SingleOrDefaultAsync(
                chapter => chapter.UserId == userId &&
                           chapter.PrivateBookId == privateBookId &&
                           chapter.Id == privateChapterId,
                cancellationToken)
            .ConfigureAwait(false);

        return entity is null ? null : ToDomain(entity);
    }

    private static PrivateBookEntity ToEntity(PrivateBook book) => new()
    {
        UserId = book.UserId,
        Id = book.Id,
        Title = book.Title,
        Author = book.Author,
        CreatedAt = book.CreatedAt,
        UpdatedAt = book.UpdatedAt,
    };

    private static PrivateBook ToDomain(PrivateBookEntity entity) =>
        PrivateBook.Rehydrate(
            entity.UserId,
            entity.Id,
            entity.Title,
            entity.Author,
            entity.CreatedAt,
            entity.UpdatedAt);

    private static PrivateChapterEntity ToEntity(PrivateChapter chapter) => new()
    {
        UserId = chapter.UserId,
        PrivateBookId = chapter.PrivateBookId,
        Id = chapter.Id,
        ChapterIndex = chapter.Index,
        Title = chapter.Title,
        ContentText = chapter.ContentText,
        ContentHash = chapter.ContentHash,
        ParagraphCount = chapter.ParagraphCount,
        CreatedAt = chapter.CreatedAt,
    };

    private static PrivateChapter ToDomain(PrivateChapterEntity entity) =>
        PrivateChapter.Rehydrate(
            entity.UserId,
            entity.PrivateBookId,
            entity.Id,
            entity.ChapterIndex,
            entity.Title,
            entity.ContentText,
            entity.ContentHash,
            entity.ParagraphCount,
            entity.CreatedAt);
}
