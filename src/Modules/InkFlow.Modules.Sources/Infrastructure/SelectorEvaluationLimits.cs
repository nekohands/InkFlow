namespace InkFlow.Modules.Sources.Infrastructure;

internal static class SelectorEvaluationLimits
{
    internal const int MaxDocumentBytes = 16 * 1024 * 1024;
    internal const int MaxMatches = 4_096;
    internal const int MaxJsonDepth = 64;
    internal const int MaxHtmlPathSteps = 64;
    internal const int MaxHtmlDepth = 256;
    internal const int MaxHtmlVisitedElements = 100_000;
}
