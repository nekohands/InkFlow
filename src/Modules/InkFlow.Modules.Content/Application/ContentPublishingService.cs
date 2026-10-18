using InkFlow.Modules.Content.Domain;

namespace InkFlow.Modules.Content.Application;

public sealed record PublishOutcome(
    bool IsSuccess,
    ContentVersion? Version,
    bool Unchanged,
    IReadOnlyList<string> Errors)
{
    public static PublishOutcome Ok(ContentVersion version, bool unchanged) =>
        new(true, version, unchanged, []);
}

/// <summary>
/// 内容发布服务:原始抓取正文 → 规范化 → CanonicalHash 幂等检查 →
/// 质量评估 → 落库 → 选优当前版本。
/// "正常阅读路径不得依赖同步实时抓取"这一不变量的数据基础即本服务产出的 IsCurrent 版本。
/// </summary>
public sealed class ContentPublishingService(
    IContentVersionRepository repository,
    IContentSelectionService? selectionService = null)
{
    private readonly ContentVersionIngestor ingestor = new(repository);

    public async Task<PublishOutcome> PublishAsync(
        Guid canonicalBookId, Guid canonicalChapterId, string sourceId,
        string rawContent, CancellationToken cancellationToken = default)
    {
        var ingestion = await ingestor
            .IngestAsync(
                canonicalBookId,
                canonicalChapterId,
                sourceId,
                rawContent,
                cancellationToken)
            .ConfigureAwait(false);
        if (!ingestion.IsSuccess)
        {
            return new PublishOutcome(false, null, false,
                ingestion.Errors);
        }

        var version = ingestion.Version!;
        if (selectionService is not null)
        {
            var selection = await selectionService
                .SelectCurrentAsync(canonicalChapterId, cancellationToken)
                .ConfigureAwait(false);
            return selection.IsSuccess
                ? PublishOutcome.Ok(selection.SelectedVersion ?? version, ingestion.Unchanged)
                : new PublishOutcome(false, version, ingestion.Unchanged, selection.Errors);
        }

        if (ingestion.Unchanged)
        {
            return PublishOutcome.Ok(version, unchanged: true);
        }

        // 选优:新版本与既有版本(含自己)比较,胜者成为当前版本。
        var allVersions = await repository
            .ListForChapterAsync(canonicalChapterId, cancellationToken)
            .ConfigureAwait(false);

        var best = ContentVersionSelector.SelectBest(allVersions);

        if (best is not null && !best.IsCurrent)
        {
            await repository
                .SetCurrentAsync(canonicalChapterId, best.Id, cancellationToken)
                .ConfigureAwait(false);
        }

        return PublishOutcome.Ok(best ?? version, unchanged: false);
    }
}
