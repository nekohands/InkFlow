using InkFlow.Modules.Library.Application;
using InkFlow.Modules.Reading.Domain;

namespace InkFlow.Modules.Reading.Application;

/// <summary>阅读历史的可见性查询与章节元数据投影。</summary>
internal sealed class ReadingHistoryQuery(
    IReadingStateRepository repository,
    ICanonicalBookRepository books,
    ReadingBookVisibility visibleBooks)
{
    public async Task<IReadOnlyList<ReadingHistoryItem>> ListAsync(
        Guid userId,
        int limit,
        CancellationToken cancellationToken)
    {
        ValidateUser(userId);
        var entries = await repository
            .ListHistoryAsync(userId, limit, cancellationToken)
            .ConfigureAwait(false);
        var result = new List<ReadingHistoryItem>(entries.Count);

        foreach (var entry in entries)
        {
            var summary = await visibleBooks
                .GetAsync(entry.CanonicalBookId, cancellationToken)
                .ConfigureAwait(false);
            if (summary is null)
            {
                continue;
            }

            var chapter = await books
                .GetChapterAsync(
                    entry.CanonicalBookId,
                    entry.CanonicalChapterId,
                    cancellationToken)
                .ConfigureAwait(false);
            if (chapter is null)
            {
                continue;
            }

            result.Add(new ReadingHistoryItem(
                summary.Id,
                summary.Title,
                summary.Author,
                chapter.Id,
                chapter.Title,
                chapter.Index,
                entry.FirstReadAt,
                entry.LastReadAt));
        }

        return result;
    }

    private static void ValidateUser(Guid userId)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("user id must not be empty.", nameof(userId));
        }
    }
}
