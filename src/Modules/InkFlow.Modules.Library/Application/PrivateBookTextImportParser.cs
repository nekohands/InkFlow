using System.Text;
using System.Text.RegularExpressions;

namespace InkFlow.Modules.Library.Application;

internal static class PrivateBookTextImportParser
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private static readonly Regex EnglishChapterHeading = new(
        @"^\s*(?:##\s*)?Chapter\s+\d+\s*[:：-]\s*(?<title>.+?)\s*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex ChineseChapterHeading = new(
        @"^\s*第[0-9零一二三四五六七八九十百千万两]+(?:章|节|回)\s*(?<title>.*)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    internal static PrivateBookImportDraft Parse(byte[] bytes, string fallbackTitle)
    {
        if (bytes.Length == 0)
        {
            throw new PrivateBookImportFailure(PrivateLibraryResultStatus.InvalidFile);
        }

        var text = Decode(bytes);
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new PrivateBookImportFailure(PrivateLibraryResultStatus.InvalidFile);
        }

        var lines = text
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n');
        var title = fallbackTitle;
        string? author = null;
        var contentStart = 0;

        if (lines.Length > 0 &&
            string.Equals(lines[0].Trim(), "InkFlow Private Book v1", StringComparison.Ordinal))
        {
            contentStart = 1;
            while (contentStart < lines.Length &&
                   !string.IsNullOrWhiteSpace(lines[contentStart]))
            {
                var line = lines[contentStart].Trim();
                if (line.StartsWith("Title:", StringComparison.OrdinalIgnoreCase))
                {
                    title = line["Title:".Length..].Trim();
                }
                else if (line.StartsWith("Author:", StringComparison.OrdinalIgnoreCase))
                {
                    author = line["Author:".Length..].Trim();
                }

                contentStart++;
            }

            while (contentStart < lines.Length &&
                   string.IsNullOrWhiteSpace(lines[contentStart]))
            {
                contentStart++;
            }
        }

        var chapters = ParseChapters(lines[contentStart..]);
        return new(title, string.IsNullOrWhiteSpace(author) ? null : author, chapters);
    }

    private static string Decode(byte[] bytes)
    {
        try
        {
            using var utf8 = new StreamReader(
                new MemoryStream(bytes, writable: false),
                StrictUtf8,
                detectEncodingFromByteOrderMarks: true);
            return utf8.ReadToEnd();
        }
        catch (DecoderFallbackException)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            using var gb18030 = new StreamReader(
                new MemoryStream(bytes, writable: false),
                Encoding.GetEncoding(
                    936,
                    EncoderFallback.ExceptionFallback,
                    DecoderFallback.ExceptionFallback),
                detectEncodingFromByteOrderMarks: true);
            return gb18030.ReadToEnd();
        }
    }

    private static IReadOnlyList<PrivateChapterImportDraft> ParseChapters(
        IReadOnlyList<string> lines)
    {
        var chapters = new List<PrivateChapterImportDraft>();
        var currentTitle = "序章";
        var currentParagraphs = new List<string>();
        var sawHeading = false;

        foreach (var rawLine in lines)
        {
            if (TryParseHeading(rawLine, out var chapterTitle))
            {
                AddChapterIfPresent(chapters, currentTitle, currentParagraphs);
                currentTitle = chapterTitle;
                currentParagraphs = [];
                sawHeading = true;
                continue;
            }

            var line = rawLine.Trim();
            if (line.Length > 0)
            {
                currentParagraphs.Add(line);
            }
        }

        AddChapterIfPresent(chapters, currentTitle, currentParagraphs);
        if (!sawHeading && chapters.Count == 1)
        {
            chapters[0] = chapters[0] with { Title = "正文" };
        }

        if (chapters.Count == 0)
        {
            throw new PrivateBookImportFailure(PrivateLibraryResultStatus.InvalidFile);
        }

        return chapters;
    }

    private static void AddChapterIfPresent(
        ICollection<PrivateChapterImportDraft> chapters,
        string title,
        IReadOnlyCollection<string> paragraphs)
    {
        if (paragraphs.Count > 0)
        {
            chapters.Add(new PrivateChapterImportDraft(title, paragraphs.ToArray()));
        }
    }

    private static bool TryParseHeading(string line, out string title)
    {
        var english = EnglishChapterHeading.Match(line);
        if (english.Success)
        {
            title = english.Groups["title"].Value.Trim();
            return title.Length > 0;
        }

        var chinese = ChineseChapterHeading.Match(line);
        if (chinese.Success)
        {
            title = line.Trim();
            return title.Length > 0;
        }

        title = string.Empty;
        return false;
    }
}
