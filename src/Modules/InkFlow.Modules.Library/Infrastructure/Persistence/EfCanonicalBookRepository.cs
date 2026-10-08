using System.Data;
using InkFlow.BuildingBlocks.Persistence;
using InkFlow.Modules.Library.Application;
using InkFlow.Modules.Library.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Storage;

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
    private const string MatchWhitespaceCharacters =
        "\u0009\u000A\u000B\u000C\u000D\u0020\u0085\u00A0\u1680\u2000\u2001\u2002\u2003\u2004\u2005\u2006\u2007\u2008\u2009\u200A\u2028\u2029\u202F\u205F\u3000";

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
        return await BuildSummaryQuery()
            .Where(book => book.Id == id)
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

    public async Task<CanonicalBook?> FindByTitleAuthorAsync(
        string title, string author, CancellationToken cancellationToken = default)
    {
        // Book Matcher v1:书名+作者归一化(去空白、小写)精确匹配。
        var normalizedTitle = Normalize(title);
        var normalizedAuthor = Normalize(author);

        // Normalize in PostgreSQL so matching does not materialize every canonical book.
        var entity = await db.Books
            .FromSqlInterpolated($"""
                SELECT *
                FROM "library"."books"
                WHERE lower(translate("Title", {MatchWhitespaceCharacters}, '')) = {normalizedTitle}
                  AND lower(translate("Author", {MatchWhitespaceCharacters}, '')) = {normalizedAuthor}
                ORDER BY "CreatedAt", "Id"
                LIMIT 1
                """)
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return entity is null ? null : LibraryMapper.ToDomain(entity, []);
    }

    private static string Normalize(string value) =>
        string.Concat(value.Where(c => !char.IsWhiteSpace(c))).ToLowerInvariant();

    /// <summary>
    /// 匹配互斥作用域：单个 ReadCommitted 事务内以归一化 (title, author) 的稳定
    /// SHA-256 前缀为键取 pg_advisory_xact_lock。同一书身份的并发匹配在此串行化，
    /// 事务提交前锁不释放；本 DbContext 上的其余仓储写入共享该事务。
    /// </summary>
    public async Task<ICanonicalMatchScope> BeginTitleAuthorScopeAsync(
        string title, string author, CancellationToken cancellationToken = default)
    {
        var transaction = await db.Database
            .BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken)
            .ConfigureAwait(false);

        try
        {
            var lockKey = TitleAuthorLockKey(title, author);
            await db.Database
                .ExecuteSqlInterpolatedAsync(
                    $"SELECT pg_advisory_xact_lock({lockKey})", cancellationToken)
                .ConfigureAwait(false);
            return new EfCanonicalMatchScope(transaction);
        }
        catch
        {
            await transaction.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    internal static long TitleAuthorLockKey(string title, string author)
    {
        // 进程间稳定：归一化与哈希均为确定性计算，不依赖 PG 内置哈希。
        var material = string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"{Normalize(title)}\u0001{Normalize(author)}");
        var digest = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(material));
        return BitConverter.ToInt64(digest, 0);
    }

    private sealed class EfCanonicalMatchScope(IDbContextTransaction transaction) : ICanonicalMatchScope
    {
        private IDbContextTransaction? _transaction = transaction;

        public async Task CommitAsync(CancellationToken cancellationToken = default)
        {
            if (_transaction is { } current)
            {
                _transaction = null;
                await current.CommitAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (_transaction is { } current)
            {
                _transaction = null;
                await current.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

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
