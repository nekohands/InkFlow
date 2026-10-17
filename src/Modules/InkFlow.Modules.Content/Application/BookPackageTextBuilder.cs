namespace InkFlow.Modules.Content.Application;

internal static class BookPackageTextBuilder
{
    internal static async Task BuildAsync(
        BookPackageDocument document,
        Stream output,
        Func<int, Task> progress,
        CancellationToken cancellationToken)
    {
        using var writer = new StreamWriter(
            output,
            BookPackageFormatSupport.Utf8,
            4096,
            leaveOpen: true)
        {
            NewLine = "\n",
        };
        await writer.WriteLineAsync(document.Title).ConfigureAwait(false);
        await writer.WriteLineAsync($"作者：{document.Author}").ConfigureAwait(false);
        await writer.WriteLineAsync($"生成时间：{(document.GeneratedAt ?? DateTimeOffset.UtcNow):O}")
            .ConfigureAwait(false);
        await writer.WriteLineAsync("由 InkFlow 生成").ConfigureAwait(false);
        await writer.WriteLineAsync().ConfigureAwait(false);

        var completed = 0;
        foreach (var chapter in document.Chapters)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await writer.WriteLineAsync($"第 {chapter.Index + 1} 章 {chapter.Title}")
                .ConfigureAwait(false);
            await writer.WriteLineAsync().ConfigureAwait(false);
            await writer.WriteLineAsync(
                    BookPackageFormatSupport.NormalizeNewlines(chapter.CanonicalText))
                .ConfigureAwait(false);
            await writer.WriteLineAsync().ConfigureAwait(false);
            await writer.WriteLineAsync("\n").ConfigureAwait(false);
            await progress(++completed).ConfigureAwait(false);
        }

        await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
    }
}
