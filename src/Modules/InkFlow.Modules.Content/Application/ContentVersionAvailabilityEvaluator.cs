using InkFlow.Modules.Content.Domain;
using InkFlow.Modules.Sources.Application;
using InkFlow.Modules.Sources.Domain;

namespace InkFlow.Modules.Content.Application;

/// <summary>
/// 按候选版本顺序评估来源的正文能力可用性。
/// </summary>
internal sealed class ContentVersionAvailabilityEvaluator(
    ISourceHealthReader sourceHealth)
{
    public async Task<IReadOnlyList<(ContentVersion Version, bool IsAvailable)>> EvaluateAsync(
        IReadOnlyList<ContentVersion> versions,
        CancellationToken cancellationToken = default)
    {
        var availability = new List<(ContentVersion Version, bool IsAvailable)>(versions.Count);
        foreach (var version in versions)
        {
            var isAvailable = await sourceHealth
                .IsAvailableAsync(version.SourceId, SourceCapability.Content, cancellationToken)
                .ConfigureAwait(false);
            availability.Add((version, isAvailable));
        }

        return availability;
    }
}
