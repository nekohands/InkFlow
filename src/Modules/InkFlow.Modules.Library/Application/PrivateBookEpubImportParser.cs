using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using AngleSharp.Html.Parser;

namespace InkFlow.Modules.Library.Application;

internal static class PrivateBookEpubImportParser
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    internal static async Task<PrivateBookImportDraft> ParseAsync(
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
        var bytes = await PrivateBookImportParser.ReadBoundedAsync(
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
