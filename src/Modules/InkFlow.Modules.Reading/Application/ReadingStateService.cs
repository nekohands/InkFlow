using InkFlow.Modules.Content.Application;
using InkFlow.Modules.Library.Application;
using InkFlow.Modules.Library.Domain;
using InkFlow.Modules.Reading.Domain;

namespace InkFlow.Modules.Reading.Application;

/// <summary>
/// 用户阅读状态用例：只接受稳定 Canonical 身份，并在写入前确认书籍仍可公开读取。
/// </summary>
public sealed class ReadingStateService : IReadingStateService
{
    private readonly IReadingStateRepository repository;
    private readonly TimeProvider clock;
    private readonly ReadingBookVisibility visibleBooks;
    private readonly ReadingShelfQuery shelf;
    private readonly ReadingHistoryQuery history;
    private readonly ReadingProgressUseCases progress;
    private readonly ReaderPreferenceUseCases preferences;

    public ReadingStateService(
        IReadingStateRepository repository,
        ICanonicalBookRepository books,
        IContentPolicyReader contentPolicy,
        TimeProvider clock)
    {
        this.repository = repository;
        this.clock = clock;
        visibleBooks = new(books, contentPolicy);
        shelf = new(repository, books, visibleBooks);
        history = new(repository, books, visibleBooks);
        progress = new(repository, books, visibleBooks, clock);
        preferences = new(repository, clock);
    }

    public const int DefaultPageSize = 50;
    public const int MaxPageSize = 100;

    public Task<IReadOnlyList<ReadingShelfItem>> ListShelfAsync(
        Guid userId,
        int limit,
        CancellationToken cancellationToken = default)
        => shelf.ListAsync(userId, BoundLimit(limit), cancellationToken);

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

        var summary = await visibleBooks.GetAsync(canonicalBookId, cancellationToken)
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
            await shelf.MapAsync(userId, entry, cancellationToken, summary).ConfigureAwait(false)
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

    public Task<IReadOnlyList<ReadingHistoryItem>> ListHistoryAsync(
        Guid userId,
        int limit,
        CancellationToken cancellationToken = default)
        => history.ListAsync(userId, BoundLimit(limit), cancellationToken);

    public Task<ReadingProgressView?> GetProgressAsync(
        Guid userId,
        Guid canonicalBookId,
        CancellationToken cancellationToken = default)
        => progress.GetAsync(userId, canonicalBookId, cancellationToken);

    public Task<ReadingOperationResult<ReadingProgressView>> SaveProgressAsync(
        Guid userId,
        Guid canonicalBookId,
        Guid canonicalChapterId,
        int paragraphIndex,
        int progressPercent,
        CancellationToken cancellationToken = default)
        => progress.SaveAsync(
            userId,
            canonicalBookId,
            canonicalChapterId,
            paragraphIndex,
            progressPercent,
            cancellationToken);

    public Task<ReaderPreferenceView> GetPreferencesAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
        => preferences.GetAsync(userId, cancellationToken);

    public Task<ReadingOperationResult<ReaderPreferenceView>> UpdatePreferencesAsync(
        Guid userId,
        int? fontSizePercent,
        int? lineHeightPercent,
        ReaderTheme? theme,
        CancellationToken cancellationToken = default)
        => preferences
            .UpdateAsync(
                userId,
                fontSizePercent,
                lineHeightPercent,
                theme,
                cancellationToken);

    private static int BoundLimit(int limit) => Math.Clamp(limit, 1, MaxPageSize);

}
