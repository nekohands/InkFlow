using InkFlow.Modules.Sources.Application;
using InkFlow.Modules.Sources.Domain;

namespace InkFlow.Modules.Sources.Infrastructure;

/// <summary>Validates the common selector input and dispatches to one bounded language evaluator.</summary>
public sealed class RuleSelectorEvaluator : ISelectorEvaluator
{
    private readonly IReadOnlyDictionary<SelectorKind, ISelectorLanguageEvaluator> _evaluators;

    public RuleSelectorEvaluator()
    {
        _evaluators = new ISelectorLanguageEvaluator[]
        {
            new CssSelectorEvaluator(),
            new HtmlXPathSelectorEvaluator(),
            new JsonPathSelectorEvaluator(),
        }.ToDictionary(evaluator => evaluator.Kind);
    }

    public string? EvaluateFirst(
        string documentBody,
        RuleSelector selector,
        string? attributeName = null)
    {
        if (!IsUsable(documentBody, selector) ||
            !_evaluators.TryGetValue(selector.Kind, out var evaluator))
        {
            return null;
        }

        return evaluator.EvaluateFirst(documentBody, selector, attributeName);
    }

    public IReadOnlyList<SelectorElementSnapshot> SelectAll(
        string documentBody,
        RuleSelector selector)
    {
        if (!IsUsable(documentBody, selector) ||
            !_evaluators.TryGetValue(selector.Kind, out var evaluator))
        {
            return [];
        }

        return evaluator.SelectAll(documentBody, selector);
    }

    private static bool IsUsable(string documentBody, RuleSelector selector) =>
        selector is not null &&
        !string.IsNullOrWhiteSpace(documentBody) &&
        !string.IsNullOrWhiteSpace(selector.Expression);
}
