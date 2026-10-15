using InkFlow.Modules.Content.Application;
using InkFlow.Modules.Crawling.Application;
using InkFlow.Modules.Library.Application;

namespace InkFlow.Worker;

/// <summary>
/// 抓取 → 发布桥的宿主实现:经章节映射找到正典身份后调用内容发布服务。
/// 幂等性由 ContentPublishingService 的 CanonicalHash 判重保证;
/// 未映射返回 false(不算失败),基础设施/发布异常向上传播走任务重试。
/// </summary>
internal sealed class MappingContentPublisher(
    IChapterMappingRepository chapterMappings,
    ContentPublishingService publisher) : IChainedContentPublisher
{
    public async Task<bool> TryPublishAsync(
        string sourceId,
        string externalBookId,
        string externalChapterId,
        string rawContent,
        CancellationToken cancellationToken = default)
    {
        var mapping = await chapterMappings
            .FindAsync(sourceId, externalChapterId, cancellationToken)
            .ConfigureAwait(false);

        if (mapping is null)
        {
            return false;
        }

        var outcome = await publisher
            .PublishAsync(mapping.CanonicalBookId, mapping.CanonicalChapterId, sourceId, rawContent, cancellationToken)
            .ConfigureAwait(false);

        if (!outcome.IsSuccess)
        {
            throw new InvalidOperationException(
                $"publishing '{sourceId}/{externalChapterId}' failed: {string.Join("; ", outcome.Errors)}");
        }

        return true;
    }
}
