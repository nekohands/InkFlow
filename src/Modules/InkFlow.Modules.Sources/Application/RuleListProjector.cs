using InkFlow.Modules.Sources.Domain;

namespace InkFlow.Modules.Sources.Application;

/// <summary>
/// Shared bounded projection for rule capabilities that return a list.
/// Selector traversal, cancellation and result budgets belong here; capability
/// mappers only decide how one selected item becomes a domain result.
/// </summary>
internal sealed class RuleListProjector(
    ISelectorEvaluator selectorEvaluator,
    SourceRuleExecutionLimits limits)
{
    public IReadOnlyList<T> Project<T>(
        IReadOnlyList<string> responseBodies,
        RuleListBinding binding,
        CancellationToken cancellationToken,
        Func<SelectorElementSnapshot, RuleListProjection<T>?> map)
    {
        var results = new List<T>();
        long resultBytes = 0;
        var selector = new RuleSelector(binding.ItemsSelectorKind, binding.ItemsSelector);

        foreach (var body in responseBodies)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var items = selectorEvaluator.SelectAll(body, selector);
            cancellationToken.ThrowIfCancellationRequested();

            foreach (var item in items)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var projection = map(item);
                if (projection is null)
                {
                    continue;
                }

                if (resultBytes + projection.Utf8Bytes > limits.MaxResultSize ||
                    results.Count >= limits.MaxResultItems)
                {
                    return [];
                }

                resultBytes += projection.Utf8Bytes;
                results.Add(projection.Value);
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        return results;
    }
}

internal sealed record RuleListProjection<T>(T Value, long Utf8Bytes);
