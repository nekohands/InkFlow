using System.IO.Compression;
using System.Xml;

namespace InkFlow.Modules.Content.Application;

internal static class BookPackageEpubBuilder
{
    internal static async Task BuildAsync(
        BookPackageDocument document,
        Stream output,
        Func<int, Task> progress,
        CancellationToken cancellationToken)
    {
        using var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true);
        await BookPackageFormatSupport.WriteBytesEntryAsync(
                archive,
                "mimetype",
                BookPackageFormatSupport.Utf8.GetBytes("application/epub+zip"),
                CompressionLevel.NoCompression,
                cancellationToken)
            .ConfigureAwait(false);
        await BookPackageFormatSupport.WriteXmlEntryAsync(
                archive,
                "META-INF/container.xml",
                WriteContainerXml,
                cancellationToken)
            .ConfigureAwait(false);
        await BookPackageFormatSupport.WriteXmlEntryAsync(
                archive,
                "OEBPS/content.opf",
                writer => WriteOpfXml(writer, document),
                cancellationToken)
            .ConfigureAwait(false);
        await BookPackageFormatSupport.WriteXmlEntryAsync(
                archive,
                "OEBPS/nav.xhtml",
                writer => WriteNavXml(writer, document),
                cancellationToken)
            .ConfigureAwait(false);

        var completed = 0;
        foreach (var chapter in document.Chapters)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await BookPackageFormatSupport.WriteXmlEntryAsync(
                    archive,
                    $"OEBPS/chapters/{BookPackageFormatSupport.ChapterFileName(chapter)}.xhtml",
                    writer => WriteChapterXml(writer, document, chapter),
                    cancellationToken)
                .ConfigureAwait(false);
            await progress(++completed).ConfigureAwait(false);
        }
    }

    private static void WriteContainerXml(XmlWriter writer)
    {
        writer.WriteStartDocument();
        writer.WriteStartElement("container", "urn:oasis:names:tc:opendocument:xmlns:container");
        writer.WriteAttributeString("version", "1.0");
        writer.WriteStartElement("rootfiles");
        writer.WriteStartElement("rootfile");
        writer.WriteAttributeString("full-path", "OEBPS/content.opf");
        writer.WriteAttributeString("media-type", "application/oebps-package+xml");
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndDocument();
    }

    private static void WriteOpfXml(XmlWriter writer, BookPackageDocument document)
    {
        const string opf = "http://www.idpf.org/2007/opf";
        const string dc = "http://purl.org/dc/elements/1.1/";
        writer.WriteStartDocument();
        writer.WriteStartElement("package", opf);
        writer.WriteAttributeString("version", "3.0");
        writer.WriteAttributeString("unique-identifier", "book-id");

        writer.WriteStartElement("metadata", opf);
        writer.WriteStartElement("dc", "title", dc);
        writer.WriteString(document.Title);
        writer.WriteEndElement();
        writer.WriteStartElement("dc", "creator", dc);
        writer.WriteString(document.Author);
        writer.WriteEndElement();
        writer.WriteStartElement("dc", "language", dc);
        writer.WriteString("zh-CN");
        writer.WriteEndElement();
        writer.WriteStartElement("dc", "identifier", dc);
        writer.WriteAttributeString("id", "book-id");
        writer.WriteString(document.BookId.ToString("D"));
        writer.WriteEndElement();
        writer.WriteEndElement();

        writer.WriteStartElement("manifest", opf);
        writer.WriteStartElement("item", opf);
        writer.WriteAttributeString("id", "nav");
        writer.WriteAttributeString("href", "nav.xhtml");
        writer.WriteAttributeString("media-type", "application/xhtml+xml");
        writer.WriteAttributeString("properties", "nav");
        writer.WriteEndElement();
        foreach (var chapter in document.Chapters)
        {
            writer.WriteStartElement("item", opf);
            writer.WriteAttributeString("id", $"chapter-{chapter.Index}");
            writer.WriteAttributeString(
                "href",
                $"chapters/{BookPackageFormatSupport.ChapterFileName(chapter)}.xhtml");
            writer.WriteAttributeString("media-type", "application/xhtml+xml");
            writer.WriteEndElement();
        }

        writer.WriteEndElement();
        writer.WriteStartElement("spine", opf);
        foreach (var chapter in document.Chapters)
        {
            writer.WriteStartElement("itemref", opf);
            writer.WriteAttributeString("idref", $"chapter-{chapter.Index}");
            writer.WriteEndElement();
        }

        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndDocument();
    }

    private static void WriteNavXml(XmlWriter writer, BookPackageDocument document)
    {
        const string xhtml = "http://www.w3.org/1999/xhtml";
        const string epub = "http://www.idpf.org/2007/ops";
        writer.WriteStartDocument();
        writer.WriteStartElement("html", xhtml);
        writer.WriteAttributeString("xmlns", "epub", null, epub);
        writer.WriteStartElement("head", xhtml);
        writer.WriteStartElement("title", xhtml);
        writer.WriteString(document.Title);
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteStartElement("body", xhtml);
        writer.WriteStartElement("nav", xhtml);
        writer.WriteAttributeString("epub", "type", epub, "toc");
        writer.WriteStartElement("ol", xhtml);
        foreach (var chapter in document.Chapters)
        {
            writer.WriteStartElement("li", xhtml);
            writer.WriteStartElement("a", xhtml);
            writer.WriteAttributeString(
                "href",
                $"chapters/{BookPackageFormatSupport.ChapterFileName(chapter)}.xhtml");
            writer.WriteString(chapter.Title);
            writer.WriteEndElement();
            writer.WriteEndElement();
        }

        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndDocument();
    }

    private static void WriteChapterXml(
        XmlWriter writer,
        BookPackageDocument document,
        BookPackageChapter chapter)
    {
        const string xhtml = "http://www.w3.org/1999/xhtml";
        writer.WriteStartDocument();
        writer.WriteStartElement("html", xhtml);
        writer.WriteStartElement("head", xhtml);
        writer.WriteStartElement("title", xhtml);
        writer.WriteString($"{document.Title} - {chapter.Title}");
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteStartElement("body", xhtml);
        writer.WriteStartElement("h1", xhtml);
        writer.WriteString(chapter.Title);
        writer.WriteEndElement();
        foreach (var paragraph in SplitParagraphs(chapter.CanonicalText))
        {
            writer.WriteStartElement("p", xhtml);
            writer.WriteString(paragraph);
            writer.WriteEndElement();
        }

        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndDocument();
    }

    private static IEnumerable<string> SplitParagraphs(string text) =>
        BookPackageFormatSupport.NormalizeNewlines(text)
            .Split("\n\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
