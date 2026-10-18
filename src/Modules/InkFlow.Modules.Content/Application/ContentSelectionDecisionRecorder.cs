using InkFlow.Modules.Content.Domain;

namespace InkFlow.Modules.Content.Application;

/// <summary>
/// 记录正文选优的可解释证据与不可变审计决策。
/// </summary>
internal sealed class ContentSelectionDecisionRecorder(
    IContentSelectionDecisionRepository decisions,
    TimeProvider clock)
{
    public async Task<string> RecordAsync(
        Guid chapterId,
        ContentVersion selected,
        IReadOnlyList<(ContentVersion Version, bool IsAvailable)> availability,
        bool usedFallback,
        CancellationToken cancellationToken = default)
    {
        var evidence = BuildEvidence(chapterId, selected, availability, usedFallback);
        var decision = ContentSelectionDecision.Create(
            chapterId,
            selected.Id,
            evidence,
            clock.GetUtcNow());
        await decisions.AddAsync(decision, cancellationToken).ConfigureAwait(false);
        return evidence;
    }

    private static string BuildEvidence(
        Guid chapterId,
        ContentVersion selected,
        IReadOnlyList<(ContentVersion Version, bool IsAvailable)> availability,
        bool usedFallback)
    {
        var excludedSources = availability
            .Where(item => !item.IsAvailable)
            .Select(item => item.Version.SourceId)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var excludedText = excludedSources.Length == 0
            ? "none"
            : string.Join(',', excludedSources);

        var evidence =
            $"algorithm={ContentSelectionAlgorithm.Version};" +
            $"chapter={chapterId};" +
            $"candidates={availability.Count};" +
            $"eligible={availability.Count(item => item.IsAvailable)};" +
            $"excluded={availability.Count(item => !item.IsAvailable)};" +
            $"selected={selected.Id};" +
            $"selectedSource={selected.SourceId};" +
            $"fallback={usedFallback};" +
            $"excludedSources={excludedText}";

        return evidence.Length <= ContentSelectionAlgorithm.MaxEvidenceLength
            ? evidence
            : evidence[..ContentSelectionAlgorithm.MaxEvidenceLength];
    }
}
