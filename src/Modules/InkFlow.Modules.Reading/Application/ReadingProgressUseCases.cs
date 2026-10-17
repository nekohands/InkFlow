using InkFlow.Modules.Library.Application;
using InkFlow.Modules.Library.Domain;
using InkFlow.Modules.Reading.Domain;

namespace InkFlow.Modules.Reading.Application;

/// <summary>阅读进度读取、更新以及最近阅读记录的原子持久化。</summary>
internal sealed class ReadingProgressUseCases(
    IReadingStateRepository repository,
    ICanonicalBookRepository books,
    ReadingBookVisibility visibleBooks,
    TimeProvider clock)
{
    public async Task<ReadingProgressView?> GetAsync(
        Guid userId,
        Guid canonicalBookId,
        CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty || canonicalBookId == Guid.Empty)
        {
            return null;
        }

        var summary = await visibleBooks.GetAsync(canonicalBookId, cancellationToken)
            .ConfigureAwait(false);
        if (summary is null)
        {
            return null;
        }

        var progress = await repository
            .GetProgressAsync(userId, canonicalBookId, cancellationToken)
            .ConfigureAwait(false);
        if (progress is null)
        {
            return null;
        }

        var chapter = await books
            .GetChapterAsync(canonicalBookId, progress.CanonicalChapterId, cancellationToken)
            .ConfigureAwait(false);
        return MapProgress(summary, progress, chapter);
    }

    public async Task<ReadingOperationResult<ReadingProgressView>> SaveAsync(
        Guid userId,
        Guid canonicalBookId,
        Guid canonicalChapterId,
        int paragraphIndex,
        int progressPercent,
        CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty || canonicalBookId == Guid.Empty || canonicalChapterId == Guid.Empty ||
            paragraphIndex < 0 || progressPercent is < 0 or > 100)
        {
            return ReadingOperationResult<ReadingProgressView>.Failure(
                ReadingResultStatus.InvalidRequest);
        }

        var summary = await visibleBooks.GetAsync(canonicalBookId, cancellationToken)
            .ConfigureAwait(false);
        var chapter = summary is null
            ? null
            : await books.GetChapterAsync(canonicalBookId, canonicalChapterId, cancellationToken)
                .ConfigureAwait(false);
        if (summary is null || chapter is null)
        {
            return ReadingOperationResult<ReadingProgressView>.Failure(ReadingResultStatus.NotFound);
        }

        var now = clock.GetUtcNow();
        var progress = await repository
            .GetProgressAsync(userId, canonicalBookId, cancellationToken)
            .ConfigureAwait(false);
        if (progress is null)
        {
            progress = ReadingProgress.Create(
                userId,
                canonicalBookId,
                canonicalChapterId,
                paragraphIndex,
                progressPercent,
                now);
        }
        else
        {
            progress.Update(canonicalChapterId, paragraphIndex, progressPercent, now);
        }

        var history = await repository
            .GetHistoryAsync(userId, canonicalBookId, canonicalChapterId, cancellationToken)
            .ConfigureAwait(false);
        if (history is null)
        {
            history = ReadingHistoryEntry.Create(
                userId,
                canonicalBookId,
                canonicalChapterId,
                now);
        }
        else
        {
            history.Touch(now);
        }

        await repository.SaveProgressAsync(progress, history, cancellationToken)
            .ConfigureAwait(false);
        return ReadingOperationResult<ReadingProgressView>.Ok(MapProgress(summary, progress, chapter));
    }

    private static ReadingProgressView MapProgress(
        CanonicalBookSummary summary,
        ReadingProgress progress,
        CanonicalChapter? chapter)
    {
        if (chapter is null)
        {
            throw new InvalidOperationException(
                $"reading progress chapter {progress.CanonicalChapterId} does not belong to book {summary.Id}.");
        }

        return new ReadingProgressView(
            summary.Id,
            chapter.Id,
            chapter.Title,
            chapter.Index,
            progress.ParagraphIndex,
            progress.ProgressPercent,
            progress.UpdatedAt);
    }
}
