using InkFlow.Modules.Content.Domain;

namespace InkFlow.Modules.Content.Application;

internal sealed record ContentVersionIngestionResult(
    ContentVersion? Version,
    bool Unchanged,
    IReadOnlyList<string> Errors)
{
    public bool IsSuccess => Errors.Count == 0;

    public static ContentVersionIngestionResult Failed(string error) =>
        new(null, false, [error]);

    public static ContentVersionIngestionResult Succeeded(
        ContentVersion version,
        bool unchanged) =>
        new(version, unchanged, []);
}

internal sealed class ContentVersionIngestor(IContentVersionRepository repository)
{
    public async Task<ContentVersionIngestionResult> IngestAsync(
        Guid canonicalBookId,
        Guid canonicalChapterId,
        string sourceId,
        string rawContent,
        CancellationToken cancellationToken)
    {
        var document = ContentNormalizer.Normalize(rawContent);
        if (document.Paragraphs.Count == 0)
        {
            return ContentVersionIngestionResult.Failed(
                "publish: raw content normalized to an empty document.");
        }

        var canonicalHash = QualityEngine.ComputeCanonicalHash(document);
        var duplicate = await repository
            .FindByHashAsync(canonicalChapterId, canonicalHash, cancellationToken)
            .ConfigureAwait(false);

        if (duplicate is not null)
        {
            return ContentVersionIngestionResult.Succeeded(duplicate, unchanged: true);
        }

        var version = ContentVersion.Create(
            canonicalBookId,
            canonicalChapterId,
            sourceId,
            document,
            TimeProvider.System.GetUtcNow());
        await repository.AddAsync(version, cancellationToken).ConfigureAwait(false);
        return ContentVersionIngestionResult.Succeeded(version, unchanged: false);
    }
}
