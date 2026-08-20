using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace InkFlow.Modules.Content;

public abstract record ContentBlock;
public sealed record ParagraphBlock(string Text) : ContentBlock;
public sealed record HeadingBlock(string Text) : ContentBlock;
public sealed record ImageBlock(string Url, string? Alt) : ContentBlock;
public sealed record DividerBlock : ContentBlock;
public sealed record ContentDocument(IReadOnlyList<ContentBlock> Blocks)
{
    public string ToCanonicalText() => string.Join("\n\n", Blocks.Select(block => block switch
    {
        ParagraphBlock paragraph => paragraph.Text,
        HeadingBlock heading => heading.Text,
        ImageBlock image => $"[image:{image.Url}]",
        DividerBlock => "---",
        _ => string.Empty
    }).Where(value => value.Length > 0));
}

public sealed record NormalizedContent(ContentDocument Document, string CanonicalText, string CanonicalHash);

public static partial class ContentNormalizer
{
    public const string Version = "normalizer-v1";

    public static NormalizedContent FromPlainText(string input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var normalizedLines = input.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n')
            .Split('\n')
            .Select(line => WhitespaceRegex().Replace(line.Trim(), " "))
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToArray();
        var document = new ContentDocument(normalizedLines.Select(line => (ContentBlock)new ParagraphBlock(line)).ToArray());
        var canonical = document.ToCanonicalText();
        return new(document, canonical, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant());
    }

    public static string RawHash(string input) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input))).ToLowerInvariant();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)]
    private static partial Regex WhitespaceRegex();
}
