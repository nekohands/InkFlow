using InkFlow.Modules.Library.Application;
using InkFlow.Modules.Reading.Domain;

namespace InkFlow.Modules.Reading.Application;

/// <summary>书架列表的有界读取、可见性过滤与当前进度投影。</summary>
internal sealed class ReadingShelfQuery(
    IReadingStateRepository repository,
    ICanonicalBookRepository books,
    ReadingBookVisibility visibleBooks)
{
    public async Task<IReadOnlyList<ReadingShelfItem>> ListAsync(
        Guid userId,
        int limit,
        CancellationToken cancellationToken)
    {
        ValidateUser(userId);
        var entries = await repository
            .ListShelfAsync(userId, limit, cancellationToken)
            .ConfigureAwait(false);
        var result = new List<ReadingShelfItem>(entries.Count);

        foreach (var entry in entries)
        {
            var item = await MapAsync(userId, entry, cancellationToken).ConfigureAwait(false);
            if (item is not null)
            {
                result.Add(item);
            }
        }

        return result;
    }

    public async Task<ReadingShelfItem?> MapAsync(
        Guid userId,
        BookshelfEntry entry,
        CancellationToken cancellationToken,
        CanonicalBookSummary? knownSummary = null)
    {
        var summary = knownSummary ?? await visibleBooks
            .GetAsync(entry.CanonicalBookId, cancellationToken)
            .ConfigureAwait(false);
        if (summary is null)
        {
            return null;
        }

        var progress = await repository
            .GetProgressAsync(userId, entry.CanonicalBookId, cancellationToken)
            .ConfigureAwait(false);
        var chapter = progress is null
            ? null
            : await books.GetChapterAsync(
                summary.Id,
                progress.CanonicalChapterId,
                cancellationToken).ConfigureAwait(false);

        return new ReadingShelfItem(
            summary.Id,
            summary.Title,
            summary.Author,
            summary.ChapterCount,
            entry.Status.ToString(),
            entry.AddedAt,
            entry.UpdatedAt,
            chapter?.Id,
            chapter?.Title,
            progress?.ProgressPercent,
            progress?.UpdatedAt);
    }

    private static void ValidateUser(Guid userId)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("user id must not be empty.", nameof(userId));
        }
    }
}
