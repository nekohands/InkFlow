using InkFlow.Modules.Sources.Domain;

namespace InkFlow.Modules.Sources.Application;

internal sealed record FetchArtifactRecord(FetchArtifact Artifact, bool Unchanged);

/// <summary>
/// 抓取产物持久化策略：按 RawHash 判断内容是否变化，同时为复检追加新时间锚点。
/// </summary>
internal sealed class FetchArtifactRecorder(
    IFetchArtifactRepository repository,
    TimeProvider clock)
{
    public async Task<FetchArtifactRecord> RecordAsync(
        string sourceId,
        string externalBookId,
        string externalChapterId,
        string rawContent,
        CancellationToken cancellationToken)
    {
        var artifact = FetchArtifact.Capture(
            sourceId,
            externalBookId,
            externalChapterId,
            rawContent,
            clock.GetUtcNow());

        var latest = await repository
            .GetLatestAsync(sourceId, externalChapterId, cancellationToken)
            .ConfigureAwait(false);
        var unchanged = latest is not null && latest.RawHash == artifact.RawHash;

        await repository.AddAsync(artifact, cancellationToken).ConfigureAwait(false);
        return new FetchArtifactRecord(artifact, unchanged);
    }
}
