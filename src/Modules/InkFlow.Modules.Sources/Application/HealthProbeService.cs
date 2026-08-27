using InkFlow.Modules.Sources.Domain;

namespace InkFlow.Modules.Sources.Application;

/// <summary>一次主动探针的结果记录。</summary>
public sealed record HealthProbeResult(
    string SourceId,
    SourceCapability Capability,
    bool Recovered,
    string? FailureReason);

/// <summary>
/// 主动巡检式健康探测:对冷却期已满的 Unhealthy 能力主动发起轻量真实请求,
/// 成败经既有 Record* 上报——与被动半开(依赖自然流量)互补,共同构成自适应健康。
/// 
/// v1 探针范围与语义:
/// - Search:恒可探(无需样本),连通即成功,异常即失败;
/// - Toc:取该来源第一本已导入书作样本;无样本则静默跳过(没有可探目标,
///   不误报失败);空目录按目录同步同语义计失败;
/// - BookInfo/Content 不在本服务范围内:Toc 恢复后由追更链路联动覆盖。
/// 探针跳过不产生任何状态写入;成败上报交给 SourceCapabilityHealth 聚合裁定
/// (成功重置失败链回 Healthy,失败刷新锚点并延长冷却)。
/// </summary>
public sealed class HealthProbeService(
    ISourceHealthRepository healthRepository,
    ISourceHealthRecorder recorder,
    ISourceAdapterFactory adapterFactory,
    ISourceBookRepository sourceBooks,
    TimeProvider clock)
{
    /// <summary>Search 探针使用的探测关键词;探针判定的是连通性而非命中数。</summary>
    public const string ProbeKeyword = "inkflow-probe";

    public async Task<IReadOnlyList<HealthProbeResult>> ProbeDueAsync(
        CancellationToken cancellationToken = default)
    {
        var now = clock.GetUtcNow();
        var unhealthy = await healthRepository
            .ListUnhealthyAsync(cancellationToken)
            .ConfigureAwait(false);

        var results = new List<HealthProbeResult>();

        foreach (var health in unhealthy)
        {
            if (!health.IsProbeDue(now))
            {
                continue;
            }

            var adapter = await adapterFactory
                .GetAdapterAsync(health.SourceId, cancellationToken)
                .ConfigureAwait(false);
            if (adapter is null)
            {
                continue;
            }

            switch (health.Capability)
            {
                case SourceCapability.Search:
                    results.Add(await ProbeSearchAsync(adapter, health.SourceId, cancellationToken)
                        .ConfigureAwait(false));
                    break;

                case SourceCapability.Toc:
                {
                    // 无可探样本时静默跳过,不产生结果行。
                    var probe = await ProbeTocAsync(adapter, health.SourceId, cancellationToken)
                        .ConfigureAwait(false);
                    if (probe is not null)
                    {
                        results.Add(probe);
                    }

                    break;
                }
            }
        }

        return results;
    }

    private async Task<HealthProbeResult> ProbeSearchAsync(
        ISourceAdapter adapter,
        string sourceId,
        CancellationToken cancellationToken)
    {
        try
        {
            await adapter.SearchAsync(ProbeKeyword, cancellationToken).ConfigureAwait(false);
            await recorder.RecordSuccessAsync(sourceId, SourceCapability.Search, cancellationToken)
                .ConfigureAwait(false);
            return new HealthProbeResult(sourceId, SourceCapability.Search, Recovered: true, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return await ReportFailureAsync(sourceId, SourceCapability.Search, ex.Message, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private async Task<HealthProbeResult?> ProbeTocAsync(
        ISourceAdapter adapter,
        string sourceId,
        CancellationToken cancellationToken)
    {
        var sample = (await sourceBooks
            .ListAllAsync(cancellationToken)
            .ConfigureAwait(false))
            .FirstOrDefault(b => b.SourceId == sourceId);

        // 没有已导入书目就没有可探目标;静默跳过,不误报失败、不产生结果行。
        if (sample is null)
        {
            return null;
        }

        try
        {
            var entries = await adapter
                .GetTableOfContentsAsync(sample.ExternalBookId, cancellationToken)
                .ConfigureAwait(false);

            // 与目录同步同语义:空目录视为能力故障。
            if (entries is null || entries.Count == 0)
            {
                return await ReportFailureAsync(
                        sourceId, SourceCapability.Toc, "empty-toc", cancellationToken)
                    .ConfigureAwait(false);
            }

            await recorder.RecordSuccessAsync(sourceId, SourceCapability.Toc, cancellationToken)
                .ConfigureAwait(false);
            return new HealthProbeResult(sourceId, SourceCapability.Toc, Recovered: true, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return await ReportFailureAsync(
                    sourceId, SourceCapability.Toc, ex.Message, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private async Task<HealthProbeResult> ReportFailureAsync(
        string sourceId,
        SourceCapability capability,
        string reason,
        CancellationToken cancellationToken)
    {
        await recorder.RecordFailureAsync(
                sourceId, capability, $"probe: {reason}", cancellationToken)
            .ConfigureAwait(false);
        return new HealthProbeResult(sourceId, capability, Recovered: false, reason);
    }
}
