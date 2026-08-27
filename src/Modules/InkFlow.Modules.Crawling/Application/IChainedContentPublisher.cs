using InkFlow.Modules.Sources.Domain;

namespace InkFlow.Modules.Crawling.Application;

/// <summary>
/// 抓取 → 发布的宿主桥接契约。Crawler 只负责执行抓取并产出结果，
/// 不拥有 Canonical 映射或最终内容发布（领域所有权边界）；
/// 把来源章节正文翻译为正典 ContentVersion 的具体逻辑由应用宿主提供实现。
/// 实现必须幂等：重复发布相同内容不得产生新的 ContentVersion（哈希判重）。
/// </summary>
public interface IChainedContentPublisher
{
    /// <returns>true = 已发布或确认无需变更；false = 当前不可发布
    /// （如章节尚未映射到正典身份），调用方不应视为任务失败。</returns>
    Task<bool> TryPublishAsync(
        string sourceId,
        string externalBookId,
        string externalChapterId,
        string rawContent,
        CancellationToken cancellationToken = default);
}
