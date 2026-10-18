using System.Data;
using System.Security.Cryptography;
using System.Text;
using InkFlow.Modules.Library.Application;
using InkFlow.Modules.Library.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace InkFlow.Modules.Library.Infrastructure.Persistence;

/// <summary>
/// Persists the canonical title/author identity lookup and its cross-request
/// advisory-lock scope. The caller owns the returned transaction scope.
/// </summary>
internal sealed class CanonicalBookMatchStore(LibraryDbContext db)
{
    private const string MatchWhitespaceCharacters =
        "\u0009\u000A\u000B\u000C\u000D\u0020\u0085\u00A0\u1680\u2000\u2001\u2002\u2003\u2004\u2005\u2006\u2007\u2008\u2009\u200A\u2028\u2029\u202F\u205F\u3000";

    public async Task<CanonicalBook?> FindByTitleAuthorAsync(
        string title,
        string author,
        CancellationToken cancellationToken)
    {
        var normalizedTitle = Normalize(title);
        var normalizedAuthor = Normalize(author);

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

    public async Task<ICanonicalMatchScope> BeginTitleAuthorScopeAsync(
        string title,
        string author,
        CancellationToken cancellationToken)
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
        var material = string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"{Normalize(title)}\u0001{Normalize(author)}");
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(material));
        return BitConverter.ToInt64(digest, 0);
    }

    private static string Normalize(string value) =>
        string.Concat(value.Where(c => !char.IsWhiteSpace(c))).ToLowerInvariant();

    private sealed class EfCanonicalMatchScope(IDbContextTransaction transaction)
        : ICanonicalMatchScope
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
}
