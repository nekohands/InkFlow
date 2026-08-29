namespace InkFlow.Modules.Sources.Application;

/// <summary>来源能力健康状态的一次原子领域变更。</summary>
public enum SourceHealthMutationKind
{
    RecordSuccess,
    RecordFailure,
    Disable,
    Enable,
}
