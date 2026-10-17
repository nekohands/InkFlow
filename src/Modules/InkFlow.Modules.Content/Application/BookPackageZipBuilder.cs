using System.IO.Compression;
using System.Text.Json;

namespace InkFlow.Modules.Content.Application;

internal static class BookPackageZipBuilder
{
    internal static async Task BuildAsync(
        BookPackageDocument document,
        Stream output,
        Func<int, Task> progress,
        CancellationToken cancellationToken)
    {
        var generatedAt = document.GeneratedAt ?? DateTimeOffset.UtcNow;
        using var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true);
        await BookPackageFormatSupport.WriteTextEntryAsync(
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
                        path = $"chapters/{BookPackageFormatSupport.ChapterFileName(chapter)}.txt",
                        contentVersionId = chapter.ContentVersionId,
                        canonicalHash = chapter.CanonicalHash,
                    }),
                }),
                cancellationToken)
            .ConfigureAwait(false);
        await BookPackageFormatSupport.WriteTextEntryAsync(
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
                        path = $"chapters/{BookPackageFormatSupport.ChapterFileName(chapter)}.txt",
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
            await BookPackageFormatSupport.WriteTextEntryAsync(
                    archive,
                    $"chapters/{BookPackageFormatSupport.ChapterFileName(chapter)}.txt",
                    chapter.CanonicalText,
                    cancellationToken)
                .ConfigureAwait(false);
            await progress(++completed).ConfigureAwait(false);
        }
    }
}
