using InkFlow.Modules.Sources.Domain;

namespace InkFlow.Modules.Sources.Infrastructure.Persistence;

/// <summary>来源能力健康的持久化形态；(SourceId, Capability) 是稳定复合键。</summary>
public sealed class SourceCapabilityHealthEntity
{
    public string SourceId { get; set; } = null!;
    public SourceCapability Capability { get; set; }
    public SourceHealthStatus Status { get; set; }
    public int ConsecutiveFailures { get; set; }
    public DateTimeOffset? LastSuccessAt { get; set; }
    public DateTimeOffset? LastFailureAt { get; set; }
    public string? LastFailureReason { get; set; }
    public string AlgorithmVersion { get; set; } = null!;
    public DateTimeOffset UpdatedAt { get; set; }
}
