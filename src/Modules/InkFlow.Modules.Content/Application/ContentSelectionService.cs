using InkFlow.Modules.Content.Domain;
using InkFlow.Modules.Sources.Application;
using InkFlow.Modules.Sources.Domain;

namespace InkFlow.Modules.Content.Application;

/// <summary>
/// 正文选优深模块：先排除 Content 能力不可用的来源，再按质量选优；
/// 全部来源不可用时保留已有当前版本，避免阅读路径因上游故障中断。
/// </summary>
public sealed class ContentSelectionService(
    IContentVersionRepository versions,
    ISourceHealthReader sourceHealth,
    IContentSelectionDecisionRepository decisions,
    TimeProvider clock) : IContentSelectionService
{
    public async Task<ContentSelectionOutcome> SelectCurrentAsync(
        Guid canonicalChapterId,
        CancellationToken cancellationToken = default)
    {
        var allVersions = await versions
            .ListForChapterAsync(canonicalChapterId, cancellationToken)
            .ConfigureAwait(false);

        if (allVersions.Count == 0)
        {
            return ContentSelectionOutcome.Fail(
                [$"selection: chapter '{canonicalChapterId}' has no content versions."]);
        }

        var current = await versions
            .GetCurrentForChapterAsync(canonicalChapterId, cancellationToken)
            .ConfigureAwait(false);

        var availability = new List<(ContentVersion Version, bool IsAvailable)>(allVersions.Count);
        foreach (var version in allVersions)
        {
            var isAvailable = await sourceHealth
                .IsAvailableAsync(version.SourceId, SourceCapability.Content, cancellationToken)
                .ConfigureAwait(false);
            availability.Add((version, isAvailable));
        }

        var eligible = availability
            .Where(item => item.IsAvailable)
            .Select(item => item.Version)
            .ToList();
        var excluded = availability
            .Where(item => !item.IsAvailable)
            .Select(item => item.Version)
            .ToList();

        var usedFallback = false;
        IReadOnlyList<ContentVersion> candidates = eligible;
        ContentVersion? selected;

        if (candidates.Count == 0)
        {
            // 上游全断时继续提供已有事实内容；首次发布没有 current 时，
            // 仍从全部已落库版本选一个，确保章节不会永远不可读。
            usedFallback = true;
            selected = current ?? SelectBest(allVersions);
        }
        else
        {
            selected = SelectBest(candidates);
        }

        if (selected is null)
        {
            return ContentSelectionOutcome.Fail(
                [$"selection: chapter '{canonicalChapterId}' has no selectable content version."]);
        }

        var changed = current?.Id != selected.Id;
        if (changed)
        {
            await versions
                .SetCurrentAsync(canonicalChapterId, selected.Id, cancellationToken)
                .ConfigureAwait(false);
        }

        var evidence = BuildEvidence(
            canonicalChapterId,
            selected,
            availability,
            usedFallback);
        var decision = ContentSelectionDecision.Create(
            canonicalChapterId,
            selected.Id,
            evidence,
            clock.GetUtcNow());
        await decisions.AddAsync(decision, cancellationToken).ConfigureAwait(false);

        return ContentSelectionOutcome.Ok(selected, changed, usedFallback, evidence);
    }

    private static ContentVersion SelectBest(IReadOnlyList<ContentVersion> candidates)
    {
        var best = candidates[0];
        for (var i = 1; i < candidates.Count; i++)
        {
            best = ContentVersion.SelectCurrent(best, candidates[i]);
        }

        return best;
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
