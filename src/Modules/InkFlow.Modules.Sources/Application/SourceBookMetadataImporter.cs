using InkFlow.Modules.Sources.Domain;

namespace InkFlow.Modules.Sources.Application;

internal sealed class SourceBookMetadataImporter(
    SourceAdapterReader sourceReader,
    ISourceBookRepository sourceBookRepository,
    TimeProvider clock)
{
    public async Task<ImportOutcome> ImportAsync(
        string sourceId,
        string externalBookId,
        CancellationToken cancellationToken = default,
        SourceExecutionContext? executionContext = null)
    {
        var read = await sourceReader
            .ReadBookInfoAsync(sourceId, externalBookId, cancellationToken, executionContext)
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
            var created = SourceBook.Create(
                sourceId,
                externalBookId,
                read.Value!.Title,
                read.Value.Author,
                clock.GetUtcNow());
            await sourceBookRepository.AddAsync(created, cancellationToken).ConfigureAwait(false);
            return ImportOutcome.Ok(created);
        }

        existing.UpdateMetadata(read.Value!.Title, read.Value.Author, clock.GetUtcNow());
        await sourceBookRepository.SaveAsync(existing, cancellationToken).ConfigureAwait(false);
        return ImportOutcome.Ok(existing);
    }
}
