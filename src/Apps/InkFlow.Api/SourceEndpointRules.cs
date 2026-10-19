using InkFlow.Modules.Sources.Domain;

namespace InkFlow.Api;

internal static class SourceEndpointRules
{
    internal const int MaxReasonLength = 512;

    internal static bool IsValidSourceId(string sourceId) =>
        SourceIdRules.IsValid(sourceId);

    internal static bool TryNormalizeReason(string? reason, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(reason))
        {
            return false;
        }

        var candidate = reason.Trim().Replace('\r', ' ').Replace('\n', ' ');
        if (candidate.Length == 0 ||
            candidate.Length > MaxReasonLength ||
            candidate.Any(char.IsControl))
        {
            return false;
        }

        normalized = candidate;
        return true;
    }
}
