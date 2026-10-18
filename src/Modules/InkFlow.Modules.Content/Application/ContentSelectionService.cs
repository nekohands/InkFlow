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
    private readonly ContentVersionAvailabilityEvaluator availabilityEvaluator =
        new(sourceHealth);
    private readonly ContentSelectionDecisionRecorder decisionRecorder =
        new(decisions, clock);

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

        var availability = await availabilityEvaluator
            .EvaluateAsync(allVersions, cancellationToken)
            .ConfigureAwait(false);

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
            selected = current ?? ContentVersionSelector.SelectBest(allVersions);
        }
        else
        {
            selected = ContentVersionSelector.SelectBest(candidates);
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

        var evidence = await decisionRecorder
            .RecordAsync(
                canonicalChapterId,
                selected,
                availability,
                usedFallback,
                cancellationToken)
            .ConfigureAwait(false);

        return ContentSelectionOutcome.Ok(selected, changed, usedFallback, evidence);
    }
}
