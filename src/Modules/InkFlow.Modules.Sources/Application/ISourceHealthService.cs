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
