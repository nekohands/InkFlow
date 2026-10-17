using System.Text;
using System.Xml;
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
/// TXT/EPUB 导入入口。负责输入边界、格式路由、错误分类和草稿总量校验。
/// </summary>
public sealed class PrivateBookImportParser : IPrivateBookImportParser
{
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
                ? PrivateBookTextImportParser.Parse(bytes, fallbackTitle)
                : await PrivateBookEpubImportParser.ParseAsync(bytes, fallbackTitle, cancellationToken)
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

    internal static async Task<byte[]> ReadBoundedAsync(
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
}
