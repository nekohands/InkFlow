using InkFlow.Modules.Sources.Domain;

namespace InkFlow.Modules.Sources.Application;

internal sealed class SourceBookChapterSynchronizer(
    SourceAdapterReader sourceReader,
    ISourceBookRepository sourceBookRepository,
    TimeProvider clock)
{
    public async Task<ImportOutcome> SyncAsync(
        string sourceId,
        string externalBookId,
        CancellationToken cancellationToken = default,
        SourceExecutionContext? executionContext = null)
    {
        var read = await sourceReader
            .ReadTocAsync(sourceId, externalBookId, cancellationToken, executionContext)
            .ConfigureAwait(false);
        if (!read.IsSuccess)
        {
            return ImportOutcome.Fail([read.Error!]);
        }

        var existing = await sourceBookRepository
            .GetAsync(sourceId, externalBookId, cancellationToken)
            .ConfigureAwait(false);

        if (existing is null)
        {
            return ImportOutcome.Fail(
                [$"catalog: book '{sourceId}/{externalBookId}' must be imported (BookInfo) before syncing chapters."]);
        }

        existing.SyncChapters(
            read.Value!.Select(c => (c.ExternalChapterId, c.Title)),
            clock.GetUtcNow());
        await sourceBookRepository.SaveAsync(existing, cancellationToken).ConfigureAwait(false);
        return ImportOutcome.Ok(existing);
    }
}
