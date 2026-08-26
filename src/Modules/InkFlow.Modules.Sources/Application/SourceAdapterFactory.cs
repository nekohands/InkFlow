using InkFlow.Modules.Sources.Domain;

namespace InkFlow.Modules.Sources.Application;

/// <summary>
/// 适配器工厂:按来源标识解析适配器实例。
/// 规则型来源动态构建(基于 Source 聚合的 RuleDsl);代码型来源由 DI 注册表提供。
/// </summary>
public interface ISourceAdapterFactory
{
    /// <summary>获取指定来源的适配器;来源不存在返回 null。</summary>
    Task<ISourceAdapter?> GetAdapterAsync(string sourceId, CancellationToken cancellationToken = default);
}

/// <summary>
/// 规则型适配器工厂:依据 Source 聚合的 RuleDsl 动态构建 <see cref="RuleBasedSourceAdapter"/>。
/// 代码型来源(特殊编码/签名/登录)实现 <see cref="ISourceAdapter"/> 后在此注册即可被上层统一调度。
/// </summary>
public sealed class SourceAdapterFactory(
    ISourceRepository sourceRepository,
    RuleAdapter ruleAdapter,
    IEnumerable<ISourceAdapter> customAdapters) : ISourceAdapterFactory
{
    private readonly Dictionary<string, ISourceAdapter> _custom = customAdapters.ToDictionary(a => a.SourceId, StringComparer.Ordinal);

    public async Task<ISourceAdapter?> GetAdapterAsync(string sourceId, CancellationToken cancellationToken = default)
    {
        if (_custom.TryGetValue(sourceId, out var custom))
        {
            return custom;
        }

        var source = await sourceRepository.GetAsync(sourceId, cancellationToken).ConfigureAwait(false);
        if (source?.RuleDsl is null)
        {
            return null;
        }

        return new RuleBasedSourceAdapter(source, ruleAdapter);
    }
}
