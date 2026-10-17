using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using InkFlow.Modules.Library.Domain;

namespace InkFlow.Modules.Library.Application;

/// <summary>私有导入内容的用户范围读取与导出服务。</summary>
public sealed class PrivateLibraryContentService(
    IPrivateBookRepository repository,
    IPrivateBookImportParser parser,
    TimeProvider clock) : IPrivateLibraryContentService
{
    private readonly PrivateBookImportUseCase importer = new(repository, parser, clock);
    private readonly PrivateBookExportUseCase exporter = new(repository);

    public Task<PrivateLibraryOperationResult<PrivateBookImportView>> ImportAsync(
        Guid userId,
        string fileName,
        string? contentType,
        Stream content,
        CancellationToken cancellationToken = default)
        => importer.ImportAsync(userId, fileName, contentType, content, cancellationToken);

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

    public Task<PrivateLibraryOperationResult<PrivateLibraryExport>> ExportAsync(
        Guid userId,
        Guid privateBookId,
        string? format,
        CancellationToken cancellationToken = default)
        => exporter.ExportAsync(userId, privateBookId, format, cancellationToken);

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
