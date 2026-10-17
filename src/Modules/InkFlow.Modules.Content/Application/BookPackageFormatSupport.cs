using System.IO.Compression;
using System.Text;
using System.Xml;
using InkFlow.Modules.Content.Domain;

namespace InkFlow.Modules.Content.Application;

internal static class BookPackageFormatSupport
{
    internal static readonly Encoding Utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    internal static async Task WriteTextEntryAsync(
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

    internal static async Task WriteBytesEntryAsync(
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

    internal static async Task WriteXmlEntryAsync(
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

    internal static string ChapterFileName(BookPackageChapter chapter) =>
        $"{chapter.Index + 1:000000}";

    internal static string NormalizeNewlines(string text) =>
        text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
}
