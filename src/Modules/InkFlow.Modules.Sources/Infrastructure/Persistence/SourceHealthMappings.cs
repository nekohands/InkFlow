using InkFlow.Modules.Sources.Domain;

namespace InkFlow.Modules.Sources.Infrastructure.Persistence;

internal static class SourceHealthMapper
{
    internal static SourceCapabilityHealthEntity ToEntity(SourceCapabilityHealth health) => new()
    {
        SourceId = health.SourceId,
        Capability = health.Capability,
        Status = health.Status,
        ConsecutiveFailures = health.ConsecutiveFailures,
        LastSuccessAt = health.LastSuccessAt,
        LastFailureAt = health.LastFailureAt,
        LastFailureReason = health.LastFailureReason,
        AlgorithmVersion = health.AlgorithmVersion,
        UpdatedAt = health.UpdatedAt,
    };

    internal static SourceCapabilityHealth ToDomain(SourceCapabilityHealthEntity entity) =>
        SourceCapabilityHealth.Rehydrate(
            entity.SourceId,
            entity.Capability,
            entity.Status,
            entity.ConsecutiveFailures,
            entity.LastSuccessAt,
            entity.LastFailureAt,
            entity.LastFailureReason,
            entity.AlgorithmVersion,
            entity.UpdatedAt);

    internal static void ApplyDomain(
        SourceCapabilityHealth health,
        SourceCapabilityHealthEntity entity)
    {
        entity.Status = health.Status;
        entity.ConsecutiveFailures = health.ConsecutiveFailures;
        entity.LastSuccessAt = health.LastSuccessAt;
        entity.LastFailureAt = health.LastFailureAt;
        entity.LastFailureReason = health.LastFailureReason;
        entity.AlgorithmVersion = health.AlgorithmVersion;
        entity.UpdatedAt = health.UpdatedAt;
    }
}
