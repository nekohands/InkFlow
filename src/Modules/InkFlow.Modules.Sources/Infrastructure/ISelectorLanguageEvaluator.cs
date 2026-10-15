using InkFlow.Modules.Sources.Application;
using InkFlow.Modules.Sources.Domain;

namespace InkFlow.Modules.Sources.Infrastructure;

/// <summary>Internal seam for one bounded selector language implementation.</summary>
internal interface ISelectorLanguageEvaluator
{
    SelectorKind Kind { get; }

    string? EvaluateFirst(
        string documentBody,
        RuleSelector selector,
        string? attributeName = null);

    IReadOnlyList<SelectorElementSnapshot> SelectAll(
        string documentBody,
        RuleSelector selector);
}
