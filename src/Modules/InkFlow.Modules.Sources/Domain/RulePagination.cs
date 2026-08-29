namespace InkFlow.Modules.Sources.Domain;

/// <summary>
/// A bounded next-link pagination declaration. The first request uses the rule's
/// configured method; subsequent links are followed as GET requests on the same
/// source origin until the link is absent or a finite limit is reached.
/// </summary>
public sealed record RulePagination(
    RuleSelector NextPageSelector,
    string? NextPageAttribute = "href",
    int MaxPages = 8);
