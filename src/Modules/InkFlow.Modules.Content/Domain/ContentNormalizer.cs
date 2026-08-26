using System.Text.RegularExpressions;

namespace InkFlow.Modules.Content.Domain;

/// <summary>
/// 正文规范化器：原始抓取文本 → ContentDocument。
/// 步骤：剥 HTML 标签 → 解码基本实体 → 统一换行 → 按空行分段 → 修剪段内空白 → 丢弃空段。
/// v1 的标签剥离基于正则，足以覆盖纯段落型正文；富文本解析器后续以适配器替换。
/// </summary>
public static partial class ContentNormalizer
{
    [GeneratedRegex(@"</?(?:p|div|br|section|article)\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex BlockTagPattern();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex TagPattern();

    public static ContentDocument Normalize(string rawContent)
    {
        if (string.IsNullOrWhiteSpace(rawContent))
        {
            return ContentDocument.Empty;
        }

        // 块级标签的边界就是段落边界:先转换为换行,再剥掉其余内联标签。
        var withBreaks = BlockTagPattern().Replace(rawContent, "\n");
        var withoutTags = TagPattern().Replace(withBreaks, string.Empty)
            .Replace("&nbsp;", " ")
            .Replace("&amp;", "&")
            .Replace("&lt;", "<")
            .Replace("&gt;", ">")
            .Replace("&quot;", "\"")
            .Replace("&#39;", "'");

        var paragraphs = withoutTags
            .Replace("\r\n", "\n").Replace('\r', '\n')
            .Split("\n", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(text => text.Trim())
            .Where(text => text.Length > 0)
            .ToList();

        return new ContentDocument(
            paragraphs.Select((text, position) => new ContentParagraph(position, text)).ToList());
    }
}
