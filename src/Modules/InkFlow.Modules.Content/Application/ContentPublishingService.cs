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
    private readonly TimeProvider _clock = TimeProvider.System;

    public async Task<PublishOutcome> PublishAsync(
        Guid canonicalBookId, Guid canonicalChapterId, string sourceId,
        string rawContent, CancellationToken cancellationToken = default)
    {
        var document = ContentNormalizer.Normalize(rawContent);
        if (document.Paragraphs.Count == 0)
        {
            return new PublishOutcome(false, null, false,
                ["publish: raw content normalized to an empty document."]);
        }

        var canonicalHash = QualityEngine.ComputeCanonicalHash(document);

        var duplicate = await repository
            .FindByHashAsync(canonicalChapterId, canonicalHash, cancellationToken)
            .ConfigureAwait(false);

        if (duplicate is not null)
        {
            if (selectionService is not null)
            {
                var selection = await selectionService
                    .SelectCurrentAsync(canonicalChapterId, cancellationToken)
                    .ConfigureAwait(false);
                return selection.IsSuccess
                    ? PublishOutcome.Ok(selection.SelectedVersion ?? duplicate, unchanged: true)
                    : new PublishOutcome(false, duplicate, true, selection.Errors);
            }

            return PublishOutcome.Ok(duplicate, unchanged: true);
        }

        var version = ContentVersion.Create(
            canonicalBookId, canonicalChapterId, sourceId, document, _clock.GetUtcNow());
        await repository.AddAsync(version, cancellationToken).ConfigureAwait(false);

        if (selectionService is not null)
        {
            var selection = await selectionService
                .SelectCurrentAsync(canonicalChapterId, cancellationToken)
                .ConfigureAwait(false);
            return selection.IsSuccess
                ? PublishOutcome.Ok(selection.SelectedVersion ?? version, unchanged: false)
                : new PublishOutcome(false, version, false, selection.Errors);
        }

        // 选优:新版本与既有版本(含自己)比较,胜者成为当前版本。
        var allVersions = await repository
            .ListForChapterAsync(canonicalChapterId, cancellationToken)
            .ConfigureAwait(false);

        var best = allVersions.Aggregate(
            (ContentVersion?)null,
            (current, candidate) => current is null
                ? candidate
                : ContentVersion.SelectCurrent(current, candidate));

        if (best is not null && !best.IsCurrent)
        {
            await repository
                .SetCurrentAsync(canonicalChapterId, best.Id, cancellationToken)
                .ConfigureAwait(false);
        }

        return PublishOutcome.Ok(best ?? version, unchanged: false);
    }
}
