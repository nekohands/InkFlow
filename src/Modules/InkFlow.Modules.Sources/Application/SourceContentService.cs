using InkFlow.Modules.Sources.Domain;

namespace InkFlow.Modules.Sources.Application;

public sealed record ContentFetchOutcome(
    bool IsSuccess,
    bool Unchanged,
    FetchArtifact? Artifact,
    IReadOnlyList<string> Errors)
{
    public static ContentFetchOutcome Ok(FetchArtifact artifact, bool unchanged) =>
        new(true, unchanged, artifact, []);

    public static ContentFetchOutcome Fail(IReadOnlyList<string> errors) =>
        new(false, false, null, errors);
}

/// <summary>
/// 章节正文抓取服务:经书源兼容层获取原始正文,按 RawHash 幂等落库。
/// 上游内容未变(哈希一致)时返回 Unchanged,不产生新的存储行。
/// 正文 → Content AST / CanonicalHash 的清洗链路由 Content 模块负责,不在此处。
/// </summary>
public sealed class SourceContentService(
    ISourceAdapterFactory adapterFactory,
    ISourceBookRepository sourceBookRepository,
    IFetchArtifactRepository artifactRepository,
    TimeProvider clock)
{
    public async Task<ContentFetchOutcome> FetchChapterContentAsync(
        string sourceId, string externalBookId, string externalChapterId,
        CancellationToken cancellationToken = default)
    {
        // 前置校验全部在触网前完成:适配器、书目、章节必须已存在。
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

        var rawContent = await adapter
            .GetChapterContentAsync(externalChapterId, cancellationToken)
            .ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(rawContent))
        {
            return ContentFetchOutcome.Fail(
                [$"content: chapter '{externalChapterId}' returned no content from the source."]);
        }

        var artifact = FetchArtifact.Capture(sourceId, externalBookId, externalChapterId, rawContent, clock.GetUtcNow());

        var latest = await artifactRepository
            .GetLatestAsync(sourceId, externalChapterId, cancellationToken)
            .ConfigureAwait(false);
        if (latest is not null && latest.RawHash == artifact.RawHash)
        {
            return ContentFetchOutcome.Ok(latest, unchanged: true);
        }

        await artifactRepository.AddAsync(artifact, cancellationToken).ConfigureAwait(false);
        return ContentFetchOutcome.Ok(artifact, unchanged: false);
    }
}
