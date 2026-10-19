namespace InkFlow.Api;

internal static class SourceEndpointRules
{
    public static bool IsValidSourceId(string sourceId) =>
        !string.IsNullOrWhiteSpace(sourceId) && !sourceId.Any(char.IsWhiteSpace);
}
