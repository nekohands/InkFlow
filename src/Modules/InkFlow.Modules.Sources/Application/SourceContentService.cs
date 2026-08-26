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
/// 章节正文抓取服务：执行 Content 能力规则，把原始产物按 RawHash 幂等落库。
/// 上游内容未变（哈希一致）时返回 Unchanged，不产生新的存储行。
/// 正文 → Content AST / CanonicalHash 的清洗链路由 Content 模块负责，不在此处。
/// </summary>
public sealed class SourceContentService(
    ISourceRepository sourceRepository,
    ISourceBookRepository sourceBookRepository,
    IFetchArtifactRepository artifactRepository,
    RuleAdapter ruleAdapter)
{
    public async Task<ContentFetchOutcome> FetchChapterContentAsync(
        string sourceId, string externalBookId, string externalChapterId,
        DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        // 前置校验全部在触网前完成：来源、书目、章节必须已存在。
        var source = await sourceRepository.GetAsync(sourceId, cancellationToken).ConfigureAwait(false);
        if (source is null)
        {
            return ContentFetchOutcome.Fail([$"source '{sourceId}' does not exist."]);
        }

        var rule = source.FindRule(SourceCapability.Content);
        if (rule is null)
        {
            return ContentFetchOutcome.Fail(
                [$"source '{sourceId}' declares no rule for capability {SourceCapability.Content}."]);
        }

        var book = await sourceBookRepository
            .GetAsync(sourceId, externalBookId, cancellationToken)
            .ConfigureAwait(false);
        if (book is null)
        {
            return ContentFetchOutcome.Fail(
                [$"catalog: book '{sourceId}/{externalBookId}' has not been imported."]);
        }

        var chapterExists = book.Chapters.Any(c => c.ExternalChapterId == externalChapterId);
        if (!chapterExists)
        {
            return ContentFetchOutcome.Fail(
                [$"catalog: chapter '{externalChapterId}' is not part of book '{sourceId}/{externalBookId}'."]);
        }

        var result = await ruleAdapter
            .ExecuteAsync(
                rule, source.BaseUrl,
                new Dictionary<string, string>
                {
                    ["chapterId"] = externalChapterId,
                    ["bookId"] = externalBookId,
                },
                cancellationToken)
            .ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return ContentFetchOutcome.Fail(result.Errors);
        }

        if (!result.Values.TryGetValue("content", out var content) || string.IsNullOrWhiteSpace(content))
        {
            return ContentFetchOutcome.Fail(
                ["rules[Content]: required field 'content' missing from extraction."]);
        }

        var artifact = FetchArtifact.Capture(sourceId, externalBookId, externalChapterId, content, now);

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
