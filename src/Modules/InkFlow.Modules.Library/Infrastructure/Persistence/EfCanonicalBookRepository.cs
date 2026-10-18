using InkFlow.BuildingBlocks.Persistence;
using InkFlow.Modules.Library.Application;
using InkFlow.Modules.Library.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace InkFlow.Modules.Library.Infrastructure.Persistence;

/// <summary>领域聚合 ↔ 实体映射。</summary>
public static class LibraryMapper
{
    public static CanonicalBookEntity ToEntity(CanonicalBook book) => new()
    {
        Id = book.Id,
        Title = book.Title,
        Author = book.Author,
        CreatedAt = book.CreatedAt,
        UpdatedAt = book.UpdatedAt,
    };

    public static CanonicalChapterEntity ToEntity(CanonicalChapter chapter) => new()
    {
        Id = chapter.Id,
        BookId = chapter.BookId,
        ChapterIndex = chapter.Index,
        Title = chapter.Title,
        CreatedAt = chapter.CreatedAt,
    };

    public static CanonicalBook ToDomain(CanonicalBookEntity entity, IEnumerable<CanonicalChapterEntity> chapters) =>
        CanonicalBook.Rehydrate(
            entity.Id,
            entity.Title,
            entity.Author,
            entity.CreatedAt,
            entity.UpdatedAt,
            chapters.Select(c => new CanonicalChapter(c.Id, c.BookId, c.ChapterIndex, c.Title, c.CreatedAt)));
}

public sealed class EfCanonicalBookRepository(LibraryDbContext db) : ICanonicalBookRepository
{
    private readonly CanonicalBookMatchStore matchStore = new(db);

    public async Task AddAsync(CanonicalBook book, CancellationToken cancellationToken = default)
    {
        db.Books.Add(LibraryMapper.ToEntity(book));
        db.Chapters.AddRange(book.Chapters.Select(LibraryMapper.ToEntity));
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<CanonicalBook?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var bookEntity = await db.Books.FindAsync([id], cancellationToken).ConfigureAwait(false);
        if (bookEntity is null)
        {
            return null;
        }

        var chapters = await db.Chapters
            .Where(c => c.BookId == id)
            .OrderBy(c => c.ChapterIndex)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return LibraryMapper.ToDomain(bookEntity, chapters);
    }

    public async Task<CanonicalBookSummary?> GetSummaryAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return await db.Books
            .AsNoTracking()
            .Where(book => book.Id == id)
            .Select(book => new CanonicalBookSummary(
                book.Id,
                book.Title,
                book.Author,
                db.Chapters.Count(chapter => chapter.BookId == book.Id)))
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<CanonicalChapter?> GetChapterAsync(
        Guid bookId,
        Guid chapterId,
        CancellationToken cancellationToken = default)
    {
        var entity = await db.Chapters
            .AsNoTracking()
            .FirstOrDefaultAsync(
                chapter => chapter.BookId == bookId && chapter.Id == chapterId,
                cancellationToken)
            .ConfigureAwait(false);

        return entity is null
            ? null
            : new CanonicalChapter(
                entity.Id,
                entity.BookId,
                entity.ChapterIndex,
                entity.Title,
                entity.CreatedAt);
    }

    public async Task<IReadOnlyList<CanonicalChapterSummary>> ListChapterSummariesAsync(
        Guid bookId,
        CancellationToken cancellationToken = default)
    {
        return await db.Chapters
            .AsNoTracking()
            .Where(chapter => chapter.BookId == bookId)
            .OrderBy(chapter => chapter.ChapterIndex)
            .ThenBy(chapter => chapter.Id)
            .Select(chapter => new CanonicalChapterSummary(
                chapter.Id,
                chapter.ChapterIndex,
                chapter.Title))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyDictionary<Guid, string>> GetTitlesAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default)
    {
        var safeIds = ids
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToArray();
        if (safeIds.Length == 0)
        {
            return new Dictionary<Guid, string>();
        }

        return await db.Books
            .Where(book => safeIds.Contains(book.Id))
            .ToDictionaryAsync(book => book.Id, book => book.Title, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<CanonicalBook>> ListAsync(CancellationToken cancellationToken = default)
    {
        var entities = await db.Books
            .OrderBy(b => b.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // 列表页不含章节,只返回书目元数据。
        return entities
            .Select(e => CanonicalBook.Rehydrate(e.Id, e.Title, e.Author, e.CreatedAt, e.UpdatedAt, []))
            .ToList();
    }

    public async Task<IReadOnlyList<CanonicalBookSummary>> ListSummariesAsync(
        int limit,
        CancellationToken cancellationToken = default)
    {
        return await BuildSummaryQuery()
            .Take(Math.Clamp(limit, 1, 100))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<CanonicalBookSummary>> SearchSummariesAsync(
        string query,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var keyword = query.Trim();
        var books = db.Books.AsNoTracking();
        if (keyword.Length > 0)
        {
            var pattern = $"%{EscapeLikePattern(keyword)}%";
            books = books.Where(book =>
                EF.Functions.ILike(book.Title, pattern, "\\") ||
                EF.Functions.ILike(book.Author, pattern, "\\"));
        }

        return await books
            .OrderBy(b => b.CreatedAt)
            .ThenBy(b => b.Id)
            .Select(b => new CanonicalBookSummary(
                b.Id,
                b.Title,
                b.Author,
                db.Chapters.Count(c => c.BookId == b.Id)))
            .Take(Math.Clamp(limit, 1, 100))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private IQueryable<CanonicalBookSummary> BuildSummaryQuery() => db.Books
        .AsNoTracking()
        .OrderBy(b => b.CreatedAt)
        .ThenBy(b => b.Id)
        .Select(b => new CanonicalBookSummary(
            b.Id,
            b.Title,
            b.Author,
            db.Chapters.Count(c => c.BookId == b.Id)));

    private static string EscapeLikePattern(string value) => value
        .Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("%", "\\%", StringComparison.Ordinal)
        .Replace("_", "\\_", StringComparison.Ordinal);

    public Task<CanonicalBook?> FindByTitleAuthorAsync(
        string title, string author, CancellationToken cancellationToken = default) =>
        matchStore.FindByTitleAuthorAsync(title, author, cancellationToken);

    public Task<ICanonicalMatchScope> BeginTitleAuthorScopeAsync(
        string title, string author, CancellationToken cancellationToken = default) =>
        matchStore.BeginTitleAuthorScopeAsync(title, author, cancellationToken);

    public async Task SaveAsync(CanonicalBook book, CancellationToken cancellationToken = default)
    {
        var entity = await db.Books.FindAsync([book.Id], cancellationToken).ConfigureAwait(false)
                     ?? throw new InvalidOperationException(
                         $"canonical book {book.Id} does not exist; use {nameof(AddAsync)} first.");

        entity.Title = book.Title;
        entity.Author = book.Author;
        entity.UpdatedAt = book.UpdatedAt;

        // 章节只增不改：按 ID 找出尚不存在的章节插入。
        var existingIds = await db.Chapters
            .Where(c => c.BookId == book.Id)
            .Select(c => c.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var chapter in book.Chapters.Where(c => !existingIds.Contains(c.Id)))
        {
            db.Chapters.Add(LibraryMapper.ToEntity(chapter));
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>dotnet-ef 设计时工厂。</summary>
public sealed class LibraryDbContextFactory : IDesignTimeDbContextFactory<LibraryDbContext>
{
    public LibraryDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<LibraryDbContext>()
            .UseNpgsql("Host=localhost;Database=inkflow-design-time;Username=postgres;Password=postgres")
            .Options;

        return new LibraryDbContext(options);
    }
}

public sealed class EfMatchCandidateRepository(LibraryDbContext db) : IMatchCandidateRepository
{
    public async Task AddAsync(MatchCandidate candidate, CancellationToken cancellationToken = default)
    {
        db.MatchCandidates.Add(new MatchCandidateEntity
        {
            Id = candidate.Id,
            CanonicalBookId = candidate.CanonicalBookId,
            SourceId = candidate.SourceId,
            ExternalBookId = candidate.ExternalBookId,
            Status = (int)candidate.Status,
            CreatedAt = candidate.CreatedAt,
        });

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<MatchCandidate?> FindForSourceBookAsync(
        string sourceId, string externalBookId, CancellationToken cancellationToken = default)
    {
        var entity = await db.MatchCandidates
            .SingleOrDefaultAsync(
                c => c.SourceId == sourceId && c.ExternalBookId == externalBookId,
                cancellationToken)
            .ConfigureAwait(false);

        return entity is null
            ? null
            : new MatchCandidate(
                entity.Id, entity.CanonicalBookId, entity.SourceId, entity.ExternalBookId,
                (MatchCandidateStatus)entity.Status, entity.CreatedAt);
    }
}

public sealed class EfChapterMappingRepository(LibraryDbContext db) : IChapterMappingRepository
{
    public async Task AddAsync(ChapterMapping mapping, CancellationToken cancellationToken = default)
    {
        db.ChapterMappings.Add(new ChapterMappingEntity
        {
            Id = mapping.Id,
            SourceId = mapping.SourceId,
            ExternalChapterId = mapping.ExternalChapterId,
            SourceChapterId = mapping.SourceChapterId,
            CanonicalBookId = mapping.CanonicalBookId,
            CanonicalChapterId = mapping.CanonicalChapterId,
            CreatedAt = mapping.CreatedAt,
            AlignmentAlgorithmVersion = mapping.AlignmentAlgorithmVersion,
            AlignmentEvidence = mapping.AlignmentEvidence,
        });

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<ChapterMapping?> FindAsync(
        string sourceId, string externalChapterId, CancellationToken cancellationToken = default)
    {
        var entity = await db.ChapterMappings
            .SingleOrDefaultAsync(
                m => m.SourceId == sourceId && m.ExternalChapterId == externalChapterId,
                cancellationToken)
            .ConfigureAwait(false);

        return entity is null
            ? null
            : new ChapterMapping(
                entity.Id, entity.SourceId, entity.ExternalChapterId, entity.SourceChapterId,
                entity.CanonicalBookId, entity.CanonicalChapterId, entity.CreatedAt,
                entity.AlignmentAlgorithmVersion, entity.AlignmentEvidence);
    }
}
