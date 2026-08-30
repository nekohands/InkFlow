using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Xml;
using InkFlow.Modules.Content.Domain;

namespace InkFlow.Modules.Content.Application;

/// <summary>
/// v1 书籍打包器：只输出 UTF-8 文本，EPUB 使用 EPUB 3 的最小可阅读结构，
/// ZIP 保留一个可移植的 manifest/book/chapters 结构。
/// </summary>
public sealed class BookPackageBuilder : IBookPackageBuilder
{
    private static readonly Encoding Utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    public Task BuildAsync(
        BookPackageDocument document,
        BookPackageFormat format,
        Stream output,
        Func<int, Task> progress,
        CancellationToken cancellationToken = default) =>
        format switch
        {
            BookPackageFormat.Zip => BuildZipAsync(document, output, progress, cancellationToken),
            BookPackageFormat.Epub => BuildEpubAsync(document, output, progress, cancellationToken),
            BookPackageFormat.Txt => BuildTxtAsync(document, output, progress, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(format)),
        };

    private static async Task BuildZipAsync(
        BookPackageDocument document,
        Stream output,
        Func<int, Task> progress,
        CancellationToken cancellationToken)
    {
        var generatedAt = document.GeneratedAt ?? DateTimeOffset.UtcNow;
        using var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true);
        await WriteTextEntryAsync(
                archive,
                "manifest.json",
                JsonSerializer.Serialize(new
                {
                    format = "zip",
                    schemaVersion = "1",
                    bookId = document.BookId,
                    title = document.Title,
                    author = document.Author,
                    chapterCount = document.Chapters.Count,
                    generatedAt,
                    formatVersion = "1",
                    files = document.Chapters.Select(chapter => new
                    {
                        chapterId = chapter.Id,
                        index = chapter.Index,
                        title = chapter.Title,
                        path = $"chapters/{ChapterFileName(chapter)}.txt",
                        contentVersionId = chapter.ContentVersionId,
                        canonicalHash = chapter.CanonicalHash,
                    }),
                }),
                cancellationToken)
            .ConfigureAwait(false);
        await WriteTextEntryAsync(
                archive,
                "book.json",
                JsonSerializer.Serialize(new
                {
                    id = document.BookId,
                    title = document.Title,
                    author = document.Author,
                    generatedAt,
                    formatVersion = "1",
                    chapters = document.Chapters.Select(chapter => new
                    {
                        id = chapter.Id,
                        index = chapter.Index,
                        title = chapter.Title,
                        path = $"chapters/{ChapterFileName(chapter)}.txt",
                        contentVersionId = chapter.ContentVersionId,
                        canonicalHash = chapter.CanonicalHash,
                    }),
                }),
                cancellationToken)
            .ConfigureAwait(false);

        var completed = 0;
        foreach (var chapter in document.Chapters)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await WriteTextEntryAsync(
                    archive,
                    $"chapters/{ChapterFileName(chapter)}.txt",
                    chapter.CanonicalText,
                    cancellationToken)
                .ConfigureAwait(false);
            await progress(++completed).ConfigureAwait(false);
        }
    }

    private static async Task BuildTxtAsync(
        BookPackageDocument document,
        Stream output,
        Func<int, Task> progress,
        CancellationToken cancellationToken)
    {
        using var writer = new StreamWriter(output, Utf8, 4096, leaveOpen: true)
        {
            NewLine = "\n",
        };
        await writer.WriteLineAsync(document.Title).ConfigureAwait(false);
        await writer.WriteLineAsync($"作者：{document.Author}").ConfigureAwait(false);
        await writer.WriteLineAsync($"生成时间：{(document.GeneratedAt ?? DateTimeOffset.UtcNow):O}").ConfigureAwait(false);
        await writer.WriteLineAsync("由 InkFlow 生成").ConfigureAwait(false);
        await writer.WriteLineAsync().ConfigureAwait(false);

        var completed = 0;
        foreach (var chapter in document.Chapters)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await writer.WriteLineAsync($"第 {chapter.Index + 1} 章 {chapter.Title}").ConfigureAwait(false);
            await writer.WriteLineAsync().ConfigureAwait(false);
            await writer.WriteLineAsync(NormalizeNewlines(chapter.CanonicalText)).ConfigureAwait(false);
            await writer.WriteLineAsync().ConfigureAwait(false);
            await writer.WriteLineAsync("\n").ConfigureAwait(false);
            await progress(++completed).ConfigureAwait(false);
        }

        await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task BuildEpubAsync(
        BookPackageDocument document,
        Stream output,
        Func<int, Task> progress,
        CancellationToken cancellationToken)
    {
        using var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true);
        await WriteBytesEntryAsync(
                archive,
                "mimetype",
                Utf8.GetBytes("application/epub+zip"),
                CompressionLevel.NoCompression,
                cancellationToken)
            .ConfigureAwait(false);
        await WriteXmlEntryAsync(archive, "META-INF/container.xml", WriteContainerXml, cancellationToken)
            .ConfigureAwait(false);
        await WriteXmlEntryAsync(
                archive,
                "OEBPS/content.opf",
                writer => WriteOpfXml(writer, document),
                cancellationToken)
            .ConfigureAwait(false);
        await WriteXmlEntryAsync(
                archive,
                "OEBPS/nav.xhtml",
                writer => WriteNavXml(writer, document),
                cancellationToken)
            .ConfigureAwait(false);

        var completed = 0;
        foreach (var chapter in document.Chapters)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await WriteXmlEntryAsync(
                    archive,
                    $"OEBPS/chapters/{ChapterFileName(chapter)}.xhtml",
                    writer => WriteChapterXml(writer, document, chapter),
                    cancellationToken)
                .ConfigureAwait(false);
            await progress(++completed).ConfigureAwait(false);
        }
    }

    private static async Task WriteTextEntryAsync(
        ZipArchive archive,
        string name,
        string value,
        CancellationToken cancellationToken)
    {
        var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
        await using var stream = entry.Open();
        var bytes = Utf8.GetBytes(value);
        await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
    }

    private static async Task WriteBytesEntryAsync(
        ZipArchive archive,
        string name,
        byte[] bytes,
        CompressionLevel compressionLevel,
        CancellationToken cancellationToken)
    {
        var entry = archive.CreateEntry(name, compressionLevel);
        await using var stream = entry.Open();
        await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
    }

    private static async Task WriteXmlEntryAsync(
        ZipArchive archive,
        string name,
        Action<XmlWriter> write,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
        await using var stream = entry.Open();
        using var writer = XmlWriter.Create(stream, new XmlWriterSettings
        {
            Async = true,
            Encoding = Utf8,
            Indent = false,
        });
        write(writer);
        await writer.FlushAsync().ConfigureAwait(false);
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
            writer.WriteAttributeString("href", $"chapters/{ChapterFileName(chapter)}.xhtml");
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
            writer.WriteAttributeString("href", $"chapters/{ChapterFileName(chapter)}.xhtml");
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
        NormalizeNewlines(text)
            .Split("\n\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string NormalizeNewlines(string text) =>
        text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');

    private static string ChapterFileName(BookPackageChapter chapter) =>
        $"{chapter.Index + 1:000000}";
}
