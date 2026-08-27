using InkFlow.Modules.Sources.Domain;

namespace InkFlow.Modules.Sources.Application;

public sealed record ContentFetchOutcome(
    bool IsSuccess,
    bool Unchanged,
    FetchArtifact? Artifact,
    IReadOnlyList<string> Errors,
    string? RawContent = null)
{
    public static ContentFetchOutcome Ok(FetchArtifact artifact, bool unchanged, string? rawContent = null) =>
        new(true, unchanged, artifact, [], rawContent);

    public static ContentFetchOutcome Fail(IReadOnlyList<string> errors) =>
        new(false, false, null, errors);
}

/// <summary>
/// 章节正文抓取服务:经书源兼容层获取原始正文,按 RawHash 幂等落库。
/// 上游内容未变(哈希一致)时返回 Unchanged,并同样落一条相同哈希的复检产物行——
/// 复检本身是一次成功抓取,这让"最新产物时间"表示最近一次真实核查而非首次发现,
/// 是修订重扫保鲜判定的锚点;重复核查不会在 Content 侧产生新版本(哈希幂等)。
/// 正文 → Content AST / CanonicalHash 的清洗链路由 Content 模块负责,不在此处。
/// </summary>
public sealed class SourceContentService(
    ISourceAdapterFactory adapterFactory,
    ISourceBookRepository sourceBookRepository,
    IFetchArtifactRepository artifactRepository,
    TimeProvider clock,
    ISourceHealthReader? healthReader = null,
    ISourceHealthRecorder? healthRecorder = null)
{
    public async Task<ContentFetchOutcome> FetchChapterContentAsync(
        string sourceId, string externalBookId, string externalChapterId,
        CancellationToken cancellationToken = default)
    {
        // 前置校验全部在触网前完成:适配器、书目、章节必须已存在。
        if (healthReader is not null && !await healthReader
                .IsAvailableAsync(sourceId, SourceCapability.Content, cancellationToken)
                .ConfigureAwait(false))
        {
            return ContentFetchOutcome.Fail(
                [$"source '{sourceId}' capability Content is unavailable; retry later."]);
        }

        var adapter = await adapterFactory
            .GetAdapterAsync(sourceId, cancellationToken)
            .ConfigureAwait(false);
        if (adapter is null)
        {
            return ContentFetchOutcome.Fail([$"source '{sourceId}' does not exist or has no adapter."]);
        }

        var book = await sourceBookRepository
            .GetAsync(sourceId, externalBookId, cancellationToken)
            .ConfigureAwait(false);
        if (book is null)
        {
            return ContentFetchOutcome.Fail(
                [$"catalog: book '{sourceId}/{externalBookId}' has not been imported."]);
        }

        var chapter = book.Chapters.FirstOrDefault(c => c.ExternalChapterId == externalChapterId);
        if (chapter is null)
        {
            return ContentFetchOutcome.Fail(
                [$"catalog: chapter '{externalChapterId}' is not part of book '{sourceId}/{externalBookId}'."]);
        }

        string? rawContent;
        try
        {
            rawContent = await adapter
                .GetChapterContentAsync(externalChapterId, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            if (healthRecorder is not null)
            {
                await healthRecorder.RecordFailureAsync(
                    sourceId,
                    SourceCapability.Content,
                    "adapter-exception",
                    cancellationToken).ConfigureAwait(false);
            }

            return ContentFetchOutcome.Fail(["content: source request failed for Content."]);
        }

        if (string.IsNullOrWhiteSpace(rawContent))
        {
            if (healthRecorder is not null)
            {
                await healthRecorder.RecordFailureAsync(
                    sourceId,
                    SourceCapability.Content,
                    "empty-content",
                    cancellationToken).ConfigureAwait(false);
            }

            return ContentFetchOutcome.Fail(
                [$"content: chapter '{externalChapterId}' returned no content from the source."]);
        }

        if (healthRecorder is not null)
        {
            await healthRecorder.RecordSuccessAsync(
                sourceId, SourceCapability.Content, cancellationToken).ConfigureAwait(false);
        }

        var artifact = FetchArtifact.Capture(sourceId, externalBookId, externalChapterId, rawContent, clock.GetUtcNow());

        var latest = await artifactRepository
            .GetLatestAsync(sourceId, externalChapterId, cancellationToken)
            .ConfigureAwait(false);
        if (latest is not null && latest.RawHash == artifact.RawHash)
        {
            // 复检:相同哈希也记录本次真实抓取,续期保鲜判定的时间锚点。
            await artifactRepository.AddAsync(artifact, cancellationToken).ConfigureAwait(false);
            return ContentFetchOutcome.Ok(artifact, unchanged: true, rawContent);
        }

        await artifactRepository.AddAsync(artifact, cancellationToken).ConfigureAwait(false);
        return ContentFetchOutcome.Ok(artifact, unchanged: false, rawContent);
    }
}
