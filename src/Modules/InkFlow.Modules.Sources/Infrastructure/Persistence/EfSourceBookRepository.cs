using InkFlow.Modules.Sources.Application;
using InkFlow.Modules.Sources.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace InkFlow.Modules.Sources.Infrastructure.Persistence;

public sealed class EfSourceBookRepository(SourcesDbContext db) : ISourceBookRepository
{
    private readonly SourceBookQueryStore queryStore = new(db);
    private readonly SourceBookWriteStore writeStore = new(db);

    public Task AddAsync(SourceBook book, CancellationToken cancellationToken = default) =>
        writeStore.AddAsync(book, cancellationToken);

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

    public Task SaveAsync(SourceBook book, CancellationToken cancellationToken = default) =>
        writeStore.SaveAsync(book, cancellationToken);
}
