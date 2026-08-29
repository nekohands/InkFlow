namespace InkFlow.Modules.Sources.Domain;

/// <summary>
/// A bounded, execution-local response-cookie session for a RuleAdapter request chain.
/// It contains policy only; cookie values are never part of the persisted rule.
/// </summary>
public sealed record RuleSession(
    int MaxCookies = 32,
    int MaxCookieBytes = 4_096,
    int MaxCookieLifetimeSeconds = 3_600);
