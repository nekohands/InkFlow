using InkFlow.Modules.Content.Application;
using InkFlow.Modules.Library.Application;
using InkFlow.Modules.Library.Domain;
using InkFlow.Modules.Reading.Domain;

namespace InkFlow.Modules.Reading.Application;

/// <summary>
/// 用户阅读状态用例：只接受稳定 Canonical 身份，并在写入前确认书籍仍可公开读取。
/// </summary>
public sealed class ReadingStateService(
    IReadingStateRepository repository,
    ICanonicalBookRepository books,
    IContentPolicyReader contentPolicy,
    TimeProvider clock) : IReadingStateService
{
    public const int DefaultPageSize = 50;
    public const int MaxPageSize = 100;

    public async Task<IReadOnlyList<ReadingShelfItem>> ListShelfAsync(
        Guid userId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        ValidateUser(userId);
        var entries = await repository
            .ListShelfAsync(userId, BoundLimit(limit), cancellationToken)
            .ConfigureAwait(false);
        var result = new List<ReadingShelfItem>(entries.Count);

        foreach (var entry in entries)
        {
            var item = await MapShelfEntryAsync(userId, entry, cancellationToken).ConfigureAwait(false);
            if (item is not null)
            {
                result.Add(item);
            }
        }

        return result;
    }

    public async Task<ReadingOperationResult<ReadingShelfItem>> PutShelfAsync(
        Guid userId,
        Guid canonicalBookId,
        ShelfStatus status,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty || canonicalBookId == Guid.Empty || !Enum.IsDefined(status))
        {
            return ReadingOperationResult<ReadingShelfItem>.Failure(
                ReadingResultStatus.InvalidRequest);
        }

        var summary = await GetVisibleBookSummaryAsync(canonicalBookId, cancellationToken)
            .ConfigureAwait(false);
        if (summary is null)
        {
            return ReadingOperationResult<ReadingShelfItem>.Failure(ReadingResultStatus.NotFound);
        }

        var now = clock.GetUtcNow();
        var entry = await repository
            .GetShelfEntryAsync(userId, canonicalBookId, cancellationToken)
            .ConfigureAwait(false);
        if (entry is null)
        {
            entry = BookshelfEntry.Create(userId, canonicalBookId, status, now);
        }
        else
        {
            entry.ChangeStatus(status, now);
        }

        await repository.UpsertShelfAsync(entry, cancellationToken).ConfigureAwait(false);
        return ReadingOperationResult<ReadingShelfItem>.Ok(
            await MapShelfEntryAsync(userId, entry, cancellationToken, summary).ConfigureAwait(false)
            ?? throw new InvalidOperationException("visible shelf book disappeared during mapping."));
    }

    public async Task<ReadingResultStatus> RemoveShelfAsync(
        Guid userId,
        Guid canonicalBookId,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty || canonicalBookId == Guid.Empty)
        {
            return ReadingResultStatus.InvalidRequest;
        }

        // 删除是幂等的；即使书籍已被下架或删除，也允许用户清理自己的引用。
        await repository.RemoveShelfAsync(userId, canonicalBookId, cancellationToken)
            .ConfigureAwait(false);
        return ReadingResultStatus.Success;
    }

    public async Task<IReadOnlyList<ReadingHistoryItem>> ListHistoryAsync(
        Guid userId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        ValidateUser(userId);
        var entries = await repository
            .ListHistoryAsync(userId, BoundLimit(limit), cancellationToken)
            .ConfigureAwait(false);
        var result = new List<ReadingHistoryItem>(entries.Count);

        foreach (var entry in entries)
        {
            var summary = await books
                .GetSummaryAsync(entry.CanonicalBookId, cancellationToken)
                .ConfigureAwait(false);
            if (summary is null || await contentPolicy
                .IsTakedownAsync(entry.CanonicalBookId, cancellationToken)
                .ConfigureAwait(false))
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

    public async Task<ReadingProgressView?> GetProgressAsync(
        Guid userId,
        Guid canonicalBookId,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty || canonicalBookId == Guid.Empty)
        {
            return null;
        }

        var summary = await GetVisibleBookSummaryAsync(canonicalBookId, cancellationToken)
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

    public async Task<ReadingOperationResult<ReadingProgressView>> SaveProgressAsync(
        Guid userId,
        Guid canonicalBookId,
        Guid canonicalChapterId,
        int paragraphIndex,
        int progressPercent,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty || canonicalBookId == Guid.Empty || canonicalChapterId == Guid.Empty ||
            paragraphIndex < 0 || progressPercent is < 0 or > 100)
        {
            return ReadingOperationResult<ReadingProgressView>.Failure(
                ReadingResultStatus.InvalidRequest);
        }

        var summary = await GetVisibleBookSummaryAsync(canonicalBookId, cancellationToken)
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

    public async Task<ReaderPreferenceView> GetPreferencesAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        ValidateUser(userId);
        var preference = await repository
            .GetPreferencesAsync(userId, cancellationToken)
            .ConfigureAwait(false)
            ?? ReaderPreference.CreateDefault(userId, clock.GetUtcNow());
        return MapPreference(preference);
    }

    public async Task<ReadingOperationResult<ReaderPreferenceView>> UpdatePreferencesAsync(
        Guid userId,
        int? fontSizePercent,
        int? lineHeightPercent,
        ReaderTheme? theme,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty ||
            (fontSizePercent is null && lineHeightPercent is null && theme is null))
        {
            return ReadingOperationResult<ReaderPreferenceView>.Failure(
                ReadingResultStatus.InvalidRequest);
        }

        var preference = await repository
            .GetPreferencesAsync(userId, cancellationToken)
            .ConfigureAwait(false)
            ?? ReaderPreference.CreateDefault(userId, clock.GetUtcNow());

        var nextFontSize = fontSizePercent ?? preference.FontSizePercent;
        var nextLineHeight = lineHeightPercent ?? preference.LineHeightPercent;
        var nextTheme = theme ?? preference.Theme;
        try
        {
            preference.Update(nextFontSize, nextLineHeight, nextTheme, clock.GetUtcNow());
        }
        catch (ArgumentOutOfRangeException)
        {
            return ReadingOperationResult<ReaderPreferenceView>.Failure(
                ReadingResultStatus.InvalidRequest);
        }

        await repository.UpsertPreferencesAsync(preference, cancellationToken)
            .ConfigureAwait(false);
        return ReadingOperationResult<ReaderPreferenceView>.Ok(MapPreference(preference));
    }

    private async Task<ReadingShelfItem?> MapShelfEntryAsync(
        Guid userId,
        BookshelfEntry entry,
        CancellationToken cancellationToken,
        CanonicalBookSummary? knownSummary = null)
    {
        var summary = knownSummary ?? await GetVisibleBookSummaryAsync(
            entry.CanonicalBookId,
            cancellationToken).ConfigureAwait(false);
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

    private async Task<CanonicalBookSummary?> GetVisibleBookSummaryAsync(
        Guid canonicalBookId,
        CancellationToken cancellationToken)
    {
        var summary = await books.GetSummaryAsync(canonicalBookId, cancellationToken)
            .ConfigureAwait(false);
        return summary is null || await contentPolicy
            .IsTakedownAsync(canonicalBookId, cancellationToken)
            .ConfigureAwait(false)
            ? null
            : summary;
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

    private static ReaderPreferenceView MapPreference(ReaderPreference preference) =>
        new(
            preference.FontSizePercent,
            preference.LineHeightPercent,
            preference.Theme.ToString(),
            preference.UpdatedAt);

    private static int BoundLimit(int limit) => Math.Clamp(limit, 1, MaxPageSize);

    private static void ValidateUser(Guid userId)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("user id must not be empty.", nameof(userId));
        }
    }

}
