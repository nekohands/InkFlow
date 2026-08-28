using System.Security.Cryptography;
using System.Text;

namespace InkFlow.Modules.Library.Domain;

/// <summary>
/// 私有正文的规范化段落文档。它不携带公共 Canonical Content 的身份语义。
/// </summary>
public sealed class PrivateContentDocument
{
    public const int MaxParagraphs = 20_000;
    public const int MaxParagraphLength = 32_768;
    public const int MaxTotalCharacters = 5_000_000;

    private PrivateContentDocument(IReadOnlyList<string> paragraphs)
    {
        Paragraphs = paragraphs;
        CanonicalText = string.Join("\n\n", paragraphs);
        ContentHash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(CanonicalText)));
    }

    public IReadOnlyList<string> Paragraphs { get; }

    public string CanonicalText { get; }

    public string ContentHash { get; }

    public static PrivateContentDocument FromParagraphs(IEnumerable<string> paragraphs)
    {
        ArgumentNullException.ThrowIfNull(paragraphs);

        var normalized = new List<string>();
        foreach (var paragraph in paragraphs)
        {
            if (paragraph is null)
            {
                throw new ArgumentException(
                    "paragraph must not be null.", nameof(paragraphs));
            }

            var value = NormalizeParagraph(paragraph);
            if (value.Length > 0)
            {
                normalized.Add(value);
            }
        }

        if (normalized.Count == 0)
        {
            throw new ArgumentException(
                "private content must contain at least one paragraph.", nameof(paragraphs));
        }

        if (normalized.Count > MaxParagraphs)
        {
            throw new ArgumentException(
                $"private content must contain at most {MaxParagraphs} paragraphs.",
                nameof(paragraphs));
        }

        var totalCharacters = normalized.Sum(static paragraph => (long)paragraph.Length)
            + ((long)normalized.Count - 1) * 2;
        if (totalCharacters > MaxTotalCharacters)
        {
            throw new ArgumentException(
                $"private content must contain at most {MaxTotalCharacters} characters.",
                nameof(paragraphs));
        }

        return new PrivateContentDocument(
            Array.AsReadOnly(normalized.ToArray()));
    }

    public static PrivateContentDocument Rehydrate(
        string canonicalText,
        string contentHash,
        int paragraphCount)
    {
        if (string.IsNullOrWhiteSpace(contentHash))
        {
            throw new ArgumentException(
                "private content hash must not be empty.", nameof(contentHash));
        }

        var document = FromParagraphs(
            canonicalText.Split(
                "\n\n",
                StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));
        if (document.Paragraphs.Count != paragraphCount ||
            !string.Equals(document.ContentHash, contentHash, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "private content persistence checksum does not match its document.");
        }

        return document;
    }

    private static string NormalizeParagraph(string value)
    {
        var builder = new StringBuilder(value.Length);
        var pendingSpace = false;

        foreach (var character in value.Trim())
        {
            if (char.IsControl(character) && !char.IsWhiteSpace(character))
            {
                throw new ArgumentException(
                    "private content must not contain control characters.", nameof(value));
            }

            if (char.IsWhiteSpace(character))
            {
                pendingSpace = builder.Length > 0;
                continue;
            }

            if (pendingSpace)
            {
                builder.Append(' ');
                pendingSpace = false;
            }

            builder.Append(character);
        }

        var normalized = builder.ToString().Trim();
        if (normalized.Length > MaxParagraphLength)
        {
            throw new ArgumentException(
                $"private paragraph must contain at most {MaxParagraphLength} characters.",
                nameof(value));
        }

        return normalized;
    }
}
