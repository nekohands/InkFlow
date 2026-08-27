using InkFlow.Modules.Sources.Domain;

namespace InkFlow.Modules.Sources.Application;

/// <summary>供其他模块读取来源某一能力是否仍可作为候选。</summary>
public interface ISourceHealthReader
{
    Task<bool> IsAvailableAsync(
        string sourceId,
        SourceCapability capability,
        CancellationToken cancellationToken = default);
}

/// <summary>抓取/目录运行时记录能力成功与失败的最小接口。</summary>
public interface ISourceHealthRecorder
{
    Task<SourceCapabilityHealth> RecordSuccessAsync(
        string sourceId,
        SourceCapability capability,
        CancellationToken cancellationToken = default);

    Task<SourceCapabilityHealth> RecordFailureAsync(
        string sourceId,
        SourceCapability capability,
        string reason,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 运维侧来源健康操作端口。调用方不需要了解健康表或状态转移细节；
/// 具体权限由宿主 API policy 负责。
/// </summary>
public interface ISourceHealthOperations
{
    Task<SourceCapabilityHealth?> GetAsync(
        string sourceId,
        SourceCapability capability,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SourceCapabilityHealth>> ListForSourceAsync(
        string sourceId,
        CancellationToken cancellationToken = default);

    Task<SourceCapabilityHealth> DisableAsync(
        string sourceId,
        SourceCapability capability,
        string reason,
        CancellationToken cancellationToken = default);

    Task<SourceCapabilityHealth> EnableAsync(
        string sourceId,
        SourceCapability capability,
        CancellationToken cancellationToken = default);
}
