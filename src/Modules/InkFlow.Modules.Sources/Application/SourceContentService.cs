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
    private readonly SourceAdapterReader sourceReader =
        new(adapterFactory, healthReader, healthRecorder);
    private readonly FetchArtifactRecorder artifactRecorder =
        new(artifactRepository, clock);

    public async Task<ContentFetchOutcome> FetchChapterContentAsync(
        string sourceId, string externalBookId, string externalChapterId,
        CancellationToken cancellationToken = default,
        SourceExecutionContext? executionContext = null)
    {
        var preparation = await sourceReader
            .PrepareAsync(
                sourceId,
                SourceCapability.Content,
                executionContext,
                cancellationToken)
            .ConfigureAwait(false);
        if (!preparation.IsSuccess)
        {
            return ContentFetchOutcome.Fail([preparation.Error!]);
        }

        // 前置校验全部在触网前完成:适配器、书目、章节必须已存在。
        var lookup = await sourceBookRepository
            .GetChapterAsync(sourceId, externalBookId, externalChapterId, cancellationToken)
            .ConfigureAwait(false);
        if (!lookup.BookExists)
        {
            return ContentFetchOutcome.Fail(
                [$"catalog: book '{sourceId}/{externalBookId}' has not been imported."]);
        }

        if (lookup.Chapter is null)
        {
            return ContentFetchOutcome.Fail(
                [$"catalog: chapter '{externalChapterId}' is not part of book '{sourceId}/{externalBookId}'."]);
        }

        var read = await sourceReader
            .ReadChapterContentAsync(
                preparation,
                sourceId,
                externalChapterId,
                cancellationToken,
                executionContext)
            .ConfigureAwait(false);
        if (!read.IsSuccess)
        {
            return ContentFetchOutcome.Fail([read.Error!]);
        }

        var rawContent = read.Value!;
        var recorded = await artifactRecorder
            .RecordAsync(
                sourceId,
                externalBookId,
                externalChapterId,
                rawContent,
                cancellationToken)
            .ConfigureAwait(false);
        return ContentFetchOutcome.Ok(recorded.Artifact, recorded.Unchanged, rawContent);
    }
}
