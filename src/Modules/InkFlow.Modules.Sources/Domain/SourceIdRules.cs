namespace InkFlow.Modules.Sources.Domain;

/// <summary>来源 ID 的持久化和值对象边界规则。</summary>
public static class SourceIdRules
{
    public const int MaxLength = 128;

    public static bool IsValid(string? sourceId) =>
        !string.IsNullOrWhiteSpace(sourceId) &&
        sourceId.Length <= MaxLength &&
        !sourceId.Any(character =>
            char.IsWhiteSpace(character) || char.IsControl(character));

    public static bool TryNormalize(string? sourceId, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(sourceId))
        {
            return false;
        }

        var candidate = sourceId.Trim();
        if (!IsValid(candidate))
        {
            return false;
        }

        normalized = candidate;
        return true;
    }
}
