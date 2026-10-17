using InkFlow.Modules.Library.Application;

namespace InkFlow.Modules.Content.Application;

internal sealed class CatalogContentReader(
    ICanonicalBookRepository bookRepository,
    IContentVersionRepository versionRepository,
    IContentPolicyReader policyReader,
    IContentSelectionService? selectionService)
{
    public async Task<ChapterContent?> ReadAsync(
        Guid chapterId,
        CancellationToken cancellationToken)
    {
        // 先读取不含正文的关联书籍 ID，策略拒绝时不加载正文列。
        var canonicalBookId = await versionRepository
            .GetCurrentCanonicalBookIdAsync(chapterId, cancellationToken)
            .ConfigureAwait(false);

        if (canonicalBookId is null || await policyReader
            .IsTakedownAsync(canonicalBookId.Value, cancellationToken)
            .ConfigureAwait(false))
        {
            return null;
        }

        // 健康状态变化后，第一次正文读取负责在已落库候选中完成重选。
        // 该步骤不访问任何来源 URL；选择失败不应绕过后续的可见性门控。
        if (selectionService is not null)
        {
            var selection = await selectionService
                .SelectCurrentAsync(chapterId, cancellationToken)
                .ConfigureAwait(false);
            if (!selection.IsSuccess)
            {
                return null;
            }

            canonicalBookId = await versionRepository
                .GetCurrentCanonicalBookIdAsync(chapterId, cancellationToken)
                .ConfigureAwait(false);
            if (canonicalBookId is null || await policyReader
                .IsTakedownAsync(canonicalBookId.Value, cancellationToken)
                .ConfigureAwait(false))
            {
                return null;
            }
        }

        var version = await versionRepository
            .GetCurrentForChapterAsync(chapterId, cancellationToken)
            .ConfigureAwait(false);

        if (version is null)
        {
            return null;
        }

        // 防止元数据检查与正文加载之间发生下架时把正文返回给调用方。
        if (await policyReader
            .IsTakedownAsync(version.CanonicalBookId, cancellationToken)
            .ConfigureAwait(false))
        {
            return null;
        }

        var chapter = await bookRepository
            .GetChapterAsync(version.CanonicalBookId, chapterId, cancellationToken)
            .ConfigureAwait(false);

        var paragraphs = version.CanonicalText
            .Split("\n\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return new ChapterContent(
            chapterId,
            version.CanonicalBookId,
            chapter?.Index ?? 0,
            chapter?.Title ?? string.Empty,
            version.SourceId,
            paragraphs);
    }
}
