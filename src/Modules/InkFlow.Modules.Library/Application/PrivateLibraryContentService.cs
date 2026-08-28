using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using AngleSharp.Html.Parser;
using InkFlow.Modules.Library.Domain;

namespace InkFlow.Modules.Library.Application;

public static class PrivateBookImportLimits
{
    public const int MaxUploadBytes = 25 * 1024 * 1024;
    public const long MaxMultipartRequestBytes = MaxUploadBytes + 256L * 1024;
    public const int MaxArchiveEntries = 512;
    public const long MaxArchiveUncompressedBytes = 50L * 1024 * 1024;
    public const long MaxArchiveEntryBytes = 25L * 1024 * 1024;
    public const int MaxChapters = 2_000;
}

internal sealed class PrivateBookImportFailure(PrivateLibraryResultStatus status)
    : Exception
{
    public PrivateLibraryResultStatus Status { get; } = status;
}

/// <summary>
/// TXT/EPUB 导入解析器。只产生私有导入草稿，不接触公共 Canonical 数据。
/// </summary>
public sealed class PrivateBookImportParser : IPrivateBookImportParser
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private static readonly Regex EnglishChapterHeading = new(
        @"^\s*(?:##\s*)?Chapter\s+\d+\s*[:：-]\s*(?<title>.+?)\s*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex ChineseChapterHeading = new(
        @"^\s*第[0-9零一二三四五六七八九十百千万两]+(?:章|节|回)\s*(?<title>.*)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public async Task<PrivateBookImportParseResult> ParseAsync(
        string fileName,
        string? contentType,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fileName) || content is null)
        {
            return new(PrivateLibraryResultStatus.InvalidRequest, null);
        }

        _ = contentType;
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (extension is not ".txt" and not ".epub")
        {
            return new(PrivateLibraryResultStatus.UnsupportedFormat, null);
        }

        try
        {
            var bytes = await ReadBoundedAsync(
                    content,
                    PrivateBookImportLimits.MaxUploadBytes,
                    cancellationToken)
                .ConfigureAwait(false);

            var fallbackTitle = GetFallbackTitle(fileName);
            var draft = extension == ".txt"
                ? ParseText(bytes, fallbackTitle)
                : await ParseEpubAsync(bytes, fallbackTitle, cancellationToken)
                    .ConfigureAwait(false);

            ValidateDraftLimits(draft);
            return new(PrivateLibraryResultStatus.Success, draft);
        }
        catch (PrivateBookImportFailure failure)
        {
            return new(failure.Status, null);
        }
        catch (DecoderFallbackException)
        {
            return new(PrivateLibraryResultStatus.InvalidFile, null);
        }
        catch (XmlException)
        {
            return new(PrivateLibraryResultStatus.InvalidFile, null);
        }
        catch (InvalidDataException)
        {
            return new(PrivateLibraryResultStatus.InvalidFile, null);
        }
        catch (UriFormatException)
        {
            return new(PrivateLibraryResultStatus.InvalidFile, null);
        }
    }

    private static async Task<byte[]> ReadBoundedAsync(
        Stream content,
        int maxBytes,
        CancellationToken cancellationToken)
    {
        if (content.CanSeek && content.Length > maxBytes)
        {
            throw new PrivateBookImportFailure(PrivateLibraryResultStatus.FileTooLarge);
        }

        await using var buffer = new MemoryStream();
        var chunk = new byte[64 * 1024];
        var total = 0L;
        while (true)
        {
            var read = await content.ReadAsync(chunk, cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            total += read;
            if (total > maxBytes)
            {
                throw new PrivateBookImportFailure(PrivateLibraryResultStatus.FileTooLarge);
            }

            await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken)
                .ConfigureAwait(false);
        }

        return buffer.ToArray();
    }

    private static PrivateBookImportDraft ParseText(byte[] bytes, string fallbackTitle)
    {
        if (bytes.Length == 0)
        {
            throw new PrivateBookImportFailure(PrivateLibraryResultStatus.InvalidFile);
        }

        var text = DecodeText(bytes);
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

        var chapters = ParseTextChapters(lines[contentStart..]);
        return new(title, string.IsNullOrWhiteSpace(author) ? null : author, chapters);
    }

    private static string DecodeText(byte[] bytes)
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

    private static IReadOnlyList<PrivateChapterImportDraft> ParseTextChapters(
        IReadOnlyList<string> lines)
    {
        var chapters = new List<PrivateChapterImportDraft>();
        var currentTitle = "序章";
        var currentParagraphs = new List<string>();
        var sawHeading = false;

        foreach (var rawLine in lines)
        {
            if (TryParseChapterHeading(rawLine, out var chapterTitle))
            {
                AddTextChapterIfPresent(chapters, currentTitle, currentParagraphs);
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

        AddTextChapterIfPresent(chapters, currentTitle, currentParagraphs);
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

    private static void AddTextChapterIfPresent(
        ICollection<PrivateChapterImportDraft> chapters,
        string title,
        IReadOnlyCollection<string> paragraphs)
    {
        if (paragraphs.Count > 0)
        {
            chapters.Add(new PrivateChapterImportDraft(title, paragraphs.ToArray()));
        }
    }

    private static bool TryParseChapterHeading(string line, out string title)
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

    private static async Task<PrivateBookImportDraft> ParseEpubAsync(
        byte[] bytes,
        string fallbackTitle,
        CancellationToken cancellationToken)
    {
        using var archive = new ZipArchive(
            new MemoryStream(bytes, writable: false), ZipArchiveMode.Read);
        var entries = IndexArchive(archive);
        var containerXml = await ReadEntryTextAsync(
                RequiredEntry(entries, "META-INF/container.xml"),
                cancellationToken)
            .ConfigureAwait(false);
        var container = ParseXml(containerXml);
        var rootfile = container
            .Descendants()
            .FirstOrDefault(element =>
                string.Equals(element.Name.LocalName, "rootfile", StringComparison.OrdinalIgnoreCase))
            ?.Attribute("full-path")
            ?.Value;
        var packagePath = NormalizeArchivePath(rootfile)
            ?? throw new PrivateBookImportFailure(PrivateLibraryResultStatus.InvalidFile);

        var packageXml = await ReadEntryTextAsync(
                RequiredEntry(entries, packagePath),
                cancellationToken)
            .ConfigureAwait(false);
        var package = ParseXml(packageXml);
        var metadata = package.Descendants()
            .FirstOrDefault(element =>
                string.Equals(element.Name.LocalName, "metadata", StringComparison.OrdinalIgnoreCase));
        var title = FirstMetadataValue(metadata, "title") ?? fallbackTitle;
        var author = FirstMetadataValue(metadata, "creator");

        var manifest = new Dictionary<string, EpubManifestItem>(StringComparer.Ordinal);
        foreach (var item in package.Descendants().Where(element =>
                     string.Equals(element.Name.LocalName, "item", StringComparison.OrdinalIgnoreCase)))
        {
            var id = item.Attribute("id")?.Value?.Trim();
            var href = item.Attribute("href")?.Value?.Trim();
            var mediaType = item.Attribute("media-type")?.Value?.Trim();
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(href) ||
                string.IsNullOrWhiteSpace(mediaType))
            {
                continue;
            }

            manifest[id] = new EpubManifestItem(id, href, mediaType);
        }

        var spine = package.Descendants()
            .FirstOrDefault(element =>
                string.Equals(element.Name.LocalName, "spine", StringComparison.OrdinalIgnoreCase));
        var orderedItems = spine is null
            ? manifest.Values
                .Where(IsHtmlContent)
                .OrderBy(item => item.Href, StringComparer.Ordinal)
                .ToList()
            : spine.Elements()
                .Where(element =>
                    string.Equals(element.Name.LocalName, "itemref", StringComparison.OrdinalIgnoreCase))
                .Select(element => element.Attribute("idref")?.Value?.Trim())
                .Where(id => !string.IsNullOrWhiteSpace(id) && manifest.ContainsKey(id!))
                .Select(id => manifest[id!])
                .Where(IsHtmlContent)
                .ToList();

        var chapters = new List<PrivateChapterImportDraft>();
        var packageDirectory = DirectoryName(packagePath);
        foreach (var item in orderedItems)
        {
            var entryPath = ResolveArchivePath(packageDirectory, item.Href);
            var html = await ReadEntryTextAsync(
                    RequiredEntry(entries, entryPath),
                    cancellationToken)
                .ConfigureAwait(false);
            var parsed = ParseXhtml(html, item.Href);
            if (parsed.Paragraphs.Count > 0)
            {
                chapters.Add(new PrivateChapterImportDraft(parsed.Title, parsed.Paragraphs));
            }
        }

        if (chapters.Count == 0)
        {
            throw new PrivateBookImportFailure(PrivateLibraryResultStatus.InvalidFile);
        }

        return new(title, string.IsNullOrWhiteSpace(author) ? null : author, chapters);
    }

    private static Dictionary<string, ZipArchiveEntry> IndexArchive(ZipArchive archive)
    {
        if (archive.Entries.Count == 0 || archive.Entries.Count > PrivateBookImportLimits.MaxArchiveEntries)
        {
            throw new PrivateBookImportFailure(PrivateLibraryResultStatus.FileTooLarge);
        }

        var entries = new Dictionary<string, ZipArchiveEntry>(StringComparer.OrdinalIgnoreCase);
        var totalUncompressed = 0L;
        foreach (var entry in archive.Entries)
        {
            var path = NormalizeArchivePath(entry.FullName)
                ?? throw new PrivateBookImportFailure(PrivateLibraryResultStatus.InvalidFile);
            if (entry.Length < 0 || entry.Length > PrivateBookImportLimits.MaxArchiveEntryBytes ||
                !entries.TryAdd(path, entry))
            {
                throw new PrivateBookImportFailure(
                    entry.Length > PrivateBookImportLimits.MaxArchiveEntryBytes
                        ? PrivateLibraryResultStatus.FileTooLarge
                        : PrivateLibraryResultStatus.InvalidFile);
            }

            totalUncompressed = checked(totalUncompressed + entry.Length);
            if (totalUncompressed > PrivateBookImportLimits.MaxArchiveUncompressedBytes)
            {
                throw new PrivateBookImportFailure(PrivateLibraryResultStatus.FileTooLarge);
            }
        }

        return entries;
    }

    private static async Task<string> ReadEntryTextAsync(
        ZipArchiveEntry entry,
        CancellationToken cancellationToken)
    {
        if (entry.Length > PrivateBookImportLimits.MaxArchiveEntryBytes)
        {
            throw new PrivateBookImportFailure(PrivateLibraryResultStatus.FileTooLarge);
        }

        await using var stream = entry.Open();
        var bytes = await ReadBoundedAsync(
                stream,
                checked((int)Math.Min(
                    PrivateBookImportLimits.MaxArchiveEntryBytes,
                    int.MaxValue)),
                cancellationToken)
            .ConfigureAwait(false);
        return StrictUtf8.GetString(bytes);
    }

    private static XDocument ParseXml(string xml)
    {
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = 2_000_000,
        };
        using var textReader = new StringReader(xml);
        using var reader = XmlReader.Create(textReader, settings);
        return XDocument.Load(reader, LoadOptions.None);
    }

    private static ParsedXhtml ParseXhtml(string html, string fallbackTitle)
    {
        var document = new HtmlParser().ParseDocument(html);
        var root = document.Body ?? document.DocumentElement;
        if (root is null)
        {
            return new(fallbackTitle, []);
        }

        var blocks = root.QuerySelectorAll("h1,h2,h3,h4,h5,h6,p,li,blockquote,pre").ToList();
        if (blocks.Count == 0)
        {
            blocks = root.QuerySelectorAll("div")
                .Where(element => element.QuerySelector("h1,h2,h3,h4,h5,h6,p,li,blockquote,pre,div") is null)
                .ToList();
        }

        var paragraphs = blocks
            .Select(element => CollapseWhitespace(element.TextContent))
            .Where(text => text.Length > 0)
            .ToList();
        if (paragraphs.Count == 0)
        {
            var fallbackText = CollapseWhitespace(root.TextContent);
            if (fallbackText.Length > 0)
            {
                paragraphs.Add(fallbackText);
            }
        }

        var title = blocks
            .FirstOrDefault(element =>
                element.TagName.StartsWith("H", StringComparison.OrdinalIgnoreCase))
            ?.TextContent;
        title = string.IsNullOrWhiteSpace(title)
            ? document.Title
            : title;
        return new(
            string.IsNullOrWhiteSpace(title) ? Path.GetFileNameWithoutExtension(fallbackTitle) : title.Trim(),
            paragraphs);
    }

    private static string CollapseWhitespace(string value)
    {
        var builder = new StringBuilder(value.Length);
        var pendingSpace = false;
        foreach (var character in value.Trim())
        {
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

        return builder.ToString().Trim();
    }

    private static void ValidateDraftLimits(PrivateBookImportDraft draft)
    {
        if (draft.Chapters.Count == 0 || draft.Chapters.Count > PrivateBookImportLimits.MaxChapters)
        {
            throw new PrivateBookImportFailure(
                draft.Chapters.Count > PrivateBookImportLimits.MaxChapters
                    ? PrivateLibraryResultStatus.FileTooLarge
                    : PrivateLibraryResultStatus.InvalidFile);
        }

        var totalCharacters = draft.Chapters
            .SelectMany(chapter => chapter.Paragraphs)
            .Sum(paragraph => (long)paragraph.Length);
        if (totalCharacters > PrivateContentDocument.MaxTotalCharacters)
        {
            throw new PrivateBookImportFailure(PrivateLibraryResultStatus.FileTooLarge);
        }
    }

    private static string GetFallbackTitle(string fileName)
    {
        var name = Path.GetFileNameWithoutExtension(Path.GetFileName(fileName));
        return string.IsNullOrWhiteSpace(name) ? "导入书籍" : name.Trim();
    }

    private static string? FirstMetadataValue(XElement? metadata, string localName) =>
        metadata?.Descendants()
            .FirstOrDefault(element =>
                string.Equals(element.Name.LocalName, localName, StringComparison.OrdinalIgnoreCase))
            ?.Value
            .Trim();

    private static bool IsHtmlContent(EpubManifestItem item) =>
        string.Equals(item.MediaType, "application/xhtml+xml", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(item.MediaType, "text/html", StringComparison.OrdinalIgnoreCase);

    private static ZipArchiveEntry RequiredEntry(
        IReadOnlyDictionary<string, ZipArchiveEntry> entries,
        string path) =>
        entries.TryGetValue(path, out var entry)
            ? entry
            : throw new PrivateBookImportFailure(PrivateLibraryResultStatus.InvalidFile);

    private static string? NormalizeArchivePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var normalized = path.Replace('\\', '/');
        if (normalized.StartsWith("/", StringComparison.Ordinal) || normalized.Contains(':'))
        {
            throw new PrivateBookImportFailure(PrivateLibraryResultStatus.InvalidFile);
        }

        var segments = normalized.Split('/');
        var clean = new List<string>(segments.Length);
        foreach (var segment in segments)
        {
            if (segment.Length == 0 || segment == ".")
            {
                continue;
            }

            if (segment == ".." || segment.Any(char.IsControl))
            {
                throw new PrivateBookImportFailure(PrivateLibraryResultStatus.InvalidFile);
            }

            clean.Add(segment);
        }

        return clean.Count == 0 ? null : string.Join('/', clean);
    }

    private static string DirectoryName(string path)
    {
        var separator = path.LastIndexOf('/');
        return separator < 0 ? string.Empty : path[..separator];
    }

    private static string ResolveArchivePath(string directory, string href)
    {
        var fragment = href.IndexOf('#');
        var withoutFragment = fragment >= 0 ? href[..fragment] : href;
        if (string.IsNullOrWhiteSpace(withoutFragment))
        {
            throw new PrivateBookImportFailure(PrivateLibraryResultStatus.InvalidFile);
        }

        var decoded = Uri.UnescapeDataString(withoutFragment).Replace('\\', '/');
        if (decoded.StartsWith("/", StringComparison.Ordinal) || decoded.Contains("://") || decoded.Contains(':'))
        {
            throw new PrivateBookImportFailure(PrivateLibraryResultStatus.InvalidFile);
        }

        return NormalizeArchivePath(
                   string.IsNullOrWhiteSpace(directory)
                       ? decoded
                       : $"{directory}/{decoded}")
               ?? throw new PrivateBookImportFailure(PrivateLibraryResultStatus.InvalidFile);
    }

    private sealed record EpubManifestItem(string Id, string Href, string MediaType);
    private sealed record ParsedXhtml(string Title, IReadOnlyList<string> Paragraphs);
}

/// <summary>私有导入内容的用户范围读取与导出服务。</summary>
public sealed class PrivateLibraryContentService(
    IPrivateBookRepository repository,
    IPrivateBookImportParser parser,
    TimeProvider clock) : IPrivateLibraryContentService
{
    public async Task<PrivateLibraryOperationResult<PrivateBookImportView>> ImportAsync(
        Guid userId,
        string fileName,
        string? contentType,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            return Invalid<PrivateBookImportView>();
        }

        var parsed = await parser
            .ParseAsync(fileName, contentType, content, cancellationToken)
            .ConfigureAwait(false);
        if (parsed.Status != PrivateLibraryResultStatus.Success || parsed.Draft is null)
        {
            return new(parsed.Status, null);
        }

        try
        {
            var now = clock.GetUtcNow();
            var book = PrivateBook.Create(
                userId,
                parsed.Draft.Title,
                parsed.Draft.Author,
                now);
            var chapters = parsed.Draft.Chapters
                .Select((chapter, index) => PrivateChapter.Create(
                    userId,
                    book.Id,
                    index,
                    chapter.Title,
                    PrivateContentDocument.FromParagraphs(chapter.Paragraphs),
                    now))
                .ToArray();

            await repository.AddWithChaptersAsync(book, chapters, cancellationToken)
                .ConfigureAwait(false);
            return Success<PrivateBookImportView>(new(
                ToView(book),
                chapters.Length));
        }
        catch (ArgumentException)
        {
            return Invalid<PrivateBookImportView>();
        }
    }

    public async Task<IReadOnlyList<PrivateChapterView>> ListChaptersAsync(
        Guid userId,
        Guid privateBookId,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty || privateBookId == Guid.Empty)
        {
            return [];
        }

        var chapters = await repository
            .ListChaptersAsync(userId, privateBookId, cancellationToken)
            .ConfigureAwait(false);
        return chapters.Select(ToView).ToList();
    }

    public async Task<PrivateChapterContentView?> GetChapterAsync(
        Guid userId,
        Guid privateBookId,
        Guid privateChapterId,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty || privateBookId == Guid.Empty || privateChapterId == Guid.Empty)
        {
            return null;
        }

        var chapter = await repository
            .GetChapterAsync(userId, privateBookId, privateChapterId, cancellationToken)
            .ConfigureAwait(false);
        return chapter is null ? null : ToContentView(chapter);
    }

    public async Task<PrivateLibraryOperationResult<PrivateLibraryExport>> ExportAsync(
        Guid userId,
        Guid privateBookId,
        string? format,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty || privateBookId == Guid.Empty)
        {
            return NotFound<PrivateLibraryExport>();
        }

        if (!TryParseFormat(format, out var exportFormat))
        {
            return new(PrivateLibraryResultStatus.InvalidRequest, null);
        }

        var book = await repository.GetAsync(userId, privateBookId, cancellationToken)
            .ConfigureAwait(false);
        if (book is null)
        {
            return NotFound<PrivateLibraryExport>();
        }

        var chapters = await repository
            .ListChaptersAsync(userId, privateBookId, cancellationToken)
            .ConfigureAwait(false);
        return Success(PrivateBookExportFormatter.Export(book, chapters, exportFormat));
    }

    private static bool TryParseFormat(string? value, out PrivateLibraryExportFormat format)
    {
        format = PrivateLibraryExportFormat.Txt;
        return value is not null &&
               (string.Equals(value, "txt", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "epub", StringComparison.OrdinalIgnoreCase)) &&
               Enum.TryParse(value, ignoreCase: true, out format);
    }

    private static PrivateBookView ToView(PrivateBook book) =>
        new(book.Id, book.Title, book.Author, book.CreatedAt, book.UpdatedAt);

    private static PrivateChapterView ToView(PrivateChapter chapter) =>
        new(chapter.Id, chapter.Index, chapter.Title, chapter.ParagraphCount, chapter.CreatedAt);

    private static PrivateChapterContentView ToContentView(PrivateChapter chapter) =>
        new(
            chapter.Id,
            chapter.PrivateBookId,
            chapter.Index,
            chapter.Title,
            chapter.ContentHash,
            chapter.Content.Paragraphs);

    private static PrivateLibraryOperationResult<T> Success<T>(T value) =>
        new(PrivateLibraryResultStatus.Success, value);

    private static PrivateLibraryOperationResult<T> NotFound<T>() =>
        new(PrivateLibraryResultStatus.NotFound, default);

    private static PrivateLibraryOperationResult<T> Invalid<T>() =>
        new(PrivateLibraryResultStatus.InvalidRequest, default);
}

public enum PrivateLibraryExportFormat
{
    Txt = 1,
    Epub = 2,
}

public static class PrivateBookExportFormatter
{
    public static PrivateLibraryExport Export(
        PrivateBook book,
        IReadOnlyList<PrivateChapter> chapters,
        PrivateLibraryExportFormat format)
    {
        var ordered = chapters.OrderBy(chapter => chapter.Index).ThenBy(chapter => chapter.Id).ToList();
        return format switch
        {
            PrivateLibraryExportFormat.Txt => ExportText(book, ordered),
            PrivateLibraryExportFormat.Epub => ExportEpub(book, ordered),
            _ => throw new ArgumentOutOfRangeException(nameof(format)),
        };
    }

    private static PrivateLibraryExport ExportText(
        PrivateBook book,
        IReadOnlyList<PrivateChapter> chapters)
    {
        var builder = new StringBuilder();
        builder.AppendLine("InkFlow Private Book v1");
        builder.Append("Title: ").AppendLine(book.Title);
        if (!string.IsNullOrWhiteSpace(book.Author))
        {
            builder.Append("Author: ").AppendLine(book.Author);
        }

        builder.AppendLine();
        foreach (var chapter in chapters)
        {
            builder.Append("## Chapter ")
                .Append(chapter.Index + 1)
                .Append(": ")
                .AppendLine(chapter.Title);
            builder.AppendLine();
            foreach (var paragraph in chapter.Content.Paragraphs)
            {
                builder.AppendLine(paragraph);
                builder.AppendLine();
            }
        }

        return new(
            SafeFileName(book.Title, ".txt"),
            "text/plain; charset=utf-8",
            Encoding.UTF8.GetBytes(builder.ToString()));
    }

    private static PrivateLibraryExport ExportEpub(
        PrivateBook book,
        IReadOnlyList<PrivateChapter> chapters)
    {
        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(archive, "mimetype", "application/epub+zip", CompressionLevel.NoCompression);
            WriteEntry(
                archive,
                "META-INF/container.xml",
                "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
                "<container version=\"1.0\" xmlns=\"urn:oasis:names:tc:opendocument:xmlns:container\">" +
                "<rootfiles><rootfile full-path=\"OEBPS/content.opf\" media-type=\"application/oebps-package+xml\"/></rootfiles>" +
                "</container>");
            WriteEntry(archive, "OEBPS/content.opf", BuildPackageXml(book, chapters));
            WriteEntry(archive, "OEBPS/nav.xhtml", BuildNavigationXml(book, chapters));

            foreach (var chapter in chapters)
            {
                WriteEntry(
                    archive,
                    $"OEBPS/chapter-{chapter.Index + 1:D5}.xhtml",
                    BuildChapterXml(chapter));
            }
        }

        return new(
            SafeFileName(book.Title, ".epub"),
            "application/epub+zip",
            output.ToArray());
    }

    private static string BuildPackageXml(
        PrivateBook book,
        IReadOnlyList<PrivateChapter> chapters)
    {
        XNamespace package = "http://www.idpf.org/2007/opf";
        XNamespace dc = "http://purl.org/dc/elements/1.1/";
        var manifest = new XElement(
            package + "manifest",
            new XElement(
                package + "item",
                new XAttribute("id", "nav"),
                new XAttribute("href", "nav.xhtml"),
                new XAttribute("media-type", "application/xhtml+xml"),
                new XAttribute("properties", "nav")),
            chapters.Select(chapter => new XElement(
                package + "item",
                new XAttribute("id", $"chapter-{chapter.Index + 1}"),
                new XAttribute("href", $"chapter-{chapter.Index + 1:D5}.xhtml"),
                new XAttribute("media-type", "application/xhtml+xml"))));
        var spine = new XElement(
            package + "spine",
            chapters.Select(chapter => new XElement(
                package + "itemref",
                new XAttribute("idref", $"chapter-{chapter.Index + 1}"))));
        var metadata = new XElement(
            package + "metadata",
            new XAttribute(XNamespace.Xmlns + "dc", dc),
            new XElement(dc + "identifier", book.Id.ToString("D"), new XAttribute("id", "book-id")),
            new XElement(dc + "title", book.Title),
            new XElement(dc + "language", "zh-CN"));
        if (!string.IsNullOrWhiteSpace(book.Author))
        {
            metadata.Add(new XElement(dc + "creator", book.Author));
        }

        return new XDocument(
            new XDeclaration("1.0", "UTF-8", "yes"),
            new XElement(
                package + "package",
                new XAttribute("version", "3.0"),
                new XAttribute("unique-identifier", "book-id"),
                metadata,
                manifest,
                spine)).ToString(SaveOptions.DisableFormatting);
    }

    private static string BuildNavigationXml(
        PrivateBook book,
        IReadOnlyList<PrivateChapter> chapters)
    {
        XNamespace xhtml = "http://www.w3.org/1999/xhtml";
        XNamespace epub = "http://www.idpf.org/2007/ops";
        var list = new XElement(
            xhtml + "ol",
            chapters.Select(chapter => new XElement(
                xhtml + "li",
                new XElement(
                    xhtml + "a",
                    new XAttribute("href", $"chapter-{chapter.Index + 1:D5}.xhtml"),
                    chapter.Title))));
        return new XDocument(
            new XDeclaration("1.0", "UTF-8", "yes"),
            new XElement(
                xhtml + "html",
                new XAttribute("lang", "zh-CN"),
                new XAttribute(XNamespace.Xmlns + "epub", epub),
                new XElement(
                    xhtml + "head",
                    new XElement(xhtml + "title", book.Title)),
                new XElement(
                    xhtml + "body",
                    new XElement(xhtml + "nav", new XAttribute(epub + "type", "toc"), list))))
            .ToString(SaveOptions.DisableFormatting);
    }

    private static string BuildChapterXml(PrivateChapter chapter)
    {
        XNamespace xhtml = "http://www.w3.org/1999/xhtml";
        return new XDocument(
            new XDeclaration("1.0", "UTF-8", "yes"),
            new XElement(
                xhtml + "html",
                new XAttribute("lang", "zh-CN"),
                new XElement(
                    xhtml + "head",
                    new XElement(xhtml + "title", chapter.Title)),
                new XElement(
                    xhtml + "body",
                    new XElement(xhtml + "h1", chapter.Title),
                    chapter.Content.Paragraphs.Select(paragraph =>
                        new XElement(xhtml + "p", paragraph)))))
            .ToString(SaveOptions.DisableFormatting);
    }

    private static void WriteEntry(
        ZipArchive archive,
        string path,
        string content,
        CompressionLevel compressionLevel = CompressionLevel.Optimal)
    {
        var entry = archive.CreateEntry(path, compressionLevel);
        using var writer = new StreamWriter(
            entry.Open(),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            bufferSize: 1024,
            leaveOpen: false);
        writer.Write(content);
    }

    private static string SafeFileName(string title, string extension)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(title.Length);
        foreach (var character in title.Trim())
        {
            builder.Append(
                character is '/' or '\\' || invalid.Contains(character) || char.IsControl(character)
                    ? '_'
                    : character);
        }

        var normalized = builder.ToString().Trim(' ', '.');
        if (normalized.Length == 0)
        {
            normalized = "private-book";
        }

        return normalized[..Math.Min(normalized.Length, 100)] + extension;
    }
}
