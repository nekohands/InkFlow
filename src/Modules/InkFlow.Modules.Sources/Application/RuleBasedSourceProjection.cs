using System.Text;
using InkFlow.Modules.Sources.Domain;

namespace InkFlow.Modules.Sources.Application;

/// <summary>
/// Maps rule-selector snapshots and source URLs to Source-domain values.
/// Rule execution, HTTP and execution context policy remain with the adapter.
/// </summary>
internal static class RuleBasedSourceProjection
{
    internal static RuleListProjection<SourceSearchResult>? ProjectSearchItem(
        SelectorElementSnapshot item,
        RuleListBinding binding)
    {
        var externalId = ExtractExternalId(item, binding);
        if (string.IsNullOrEmpty(externalId))
        {
            return null;
        }

        const string unknownAuthor = "未知";
        var title = GetItemText(item, binding);
        var itemBytes = (long)Encoding.UTF8.GetByteCount(externalId) +
            Encoding.UTF8.GetByteCount(title) +
            Encoding.UTF8.GetByteCount(unknownAuthor);
        return new(
            new SourceSearchResult(externalId, title, unknownAuthor),
            itemBytes);
    }

    internal static RuleListProjection<SourceTocEntry>? ProjectTocItem(
        SelectorElementSnapshot item,
        RuleListBinding binding,
        ref int index)
    {
        var externalId = ExtractExternalId(item, binding);
        var title = GetItemText(item, binding);
        if (string.IsNullOrEmpty(externalId) || string.IsNullOrEmpty(title))
        {
            return null;
        }

        var itemBytes = (long)Encoding.UTF8.GetByteCount(externalId) +
            Encoding.UTF8.GetByteCount(title);
        return new(new SourceTocEntry(externalId, index++, title), itemBytes);
    }

    internal static bool TryExtractBookId(
        string pathTemplate,
        string actualPath,
        out string externalBookId)
    {
        externalBookId = string.Empty;
        const string placeholder = "{bookId}";
        var placeholderIndex = pathTemplate.IndexOf(placeholder, StringComparison.Ordinal);
        if (placeholderIndex < 0 ||
            pathTemplate.IndexOf('{', placeholderIndex + placeholder.Length) >= 0 ||
            pathTemplate.IndexOf('}', placeholderIndex + placeholder.Length) >= 0)
        {
            return false;
        }

        var prefix = pathTemplate[..placeholderIndex];
        var suffix = pathTemplate[(placeholderIndex + placeholder.Length)..];
        if (!actualPath.StartsWith(prefix, StringComparison.Ordinal) ||
            !actualPath.EndsWith(suffix, StringComparison.Ordinal))
        {
            return false;
        }

        var start = prefix.Length;
        var length = actualPath.Length - prefix.Length - suffix.Length;
        if (length <= 0)
        {
            return false;
        }

        var encodedId = actualPath.Substring(start, length);
        string decodedId;
        try
        {
            decodedId = Uri.UnescapeDataString(encodedId);
        }
        catch (UriFormatException)
        {
            return false;
        }

        if (decodedId.Length == 0 ||
            decodedId.Any(character => character is '/' or '\\' || char.IsControl(character)))
        {
            return false;
        }

        externalBookId = decodedId;
        return true;
    }

    private static string GetItemText(
        SelectorElementSnapshot element,
        RuleListBinding binding)
    {
        if (!string.IsNullOrWhiteSpace(binding.TextAttribute) &&
            element.Attributes.TryGetValue(binding.TextAttribute, out var attributeValue))
        {
            return attributeValue.Trim();
        }

        return element.TextContent.Trim();
    }

    private static string? ExtractExternalId(
        SelectorElementSnapshot element,
        RuleListBinding binding)
    {
        if (!element.Attributes.TryGetValue(binding.ExternalIdAttribute, out var raw))
        {
            return null;
        }

        var id = raw;
        if (binding.IdPrefixToStrip.Length > 0 &&
            id.StartsWith(binding.IdPrefixToStrip, StringComparison.Ordinal))
        {
            id = id[binding.IdPrefixToStrip.Length..];
        }

        if (binding.IdSuffixToStrip.Length > 0 &&
            id.EndsWith(binding.IdSuffixToStrip, StringComparison.Ordinal))
        {
            id = id[..^binding.IdSuffixToStrip.Length];
        }

        return id.Length == 0 ? null : id;
    }
}
