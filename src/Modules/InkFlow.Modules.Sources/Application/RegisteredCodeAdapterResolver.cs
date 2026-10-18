using InkFlow.Modules.Sources.Domain;

namespace InkFlow.Modules.Sources.Application;

/// <summary>
/// 受信任 CodeAdapter 的注册索引与启用门控。注册来源必须先通过标量启用状态读取，
/// 不能回退到完整 Source 聚合读取或 Rule DSL 分支。
/// </summary>
internal sealed class RegisteredCodeAdapterResolver(
    ISourceRepository sourceRepository,
    IEnumerable<ISourceAdapter> codeAdapters)
{
    private readonly Dictionary<string, ISourceAdapter> adapters =
        codeAdapters.ToDictionary(adapter => adapter.SourceId, StringComparer.Ordinal);

    public bool IsRegistered(string sourceId) => adapters.ContainsKey(sourceId);

    public async Task<ISourceAdapter?> ResolveAsync(
        string sourceId,
        CancellationToken cancellationToken)
    {
        if (!adapters.TryGetValue(sourceId, out var adapter))
        {
            return null;
        }

        return await sourceRepository
            .GetEnabledAsync(sourceId, cancellationToken)
            .ConfigureAwait(false) is true
            ? adapter
            : null;
    }
}
