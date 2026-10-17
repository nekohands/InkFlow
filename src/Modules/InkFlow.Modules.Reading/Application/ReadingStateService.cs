using InkFlow.Modules.Content.Application;
using InkFlow.Modules.Library.Application;
using InkFlow.Modules.Reading.Domain;

namespace InkFlow.Modules.Reading.Application;

/// <summary>
/// 用户阅读状态用例：只接受稳定 Canonical 身份，并在写入前确认书籍仍可公开读取。
/// </summary>
public sealed class ReadingStateService : IReadingStateService
{
    private readonly ReadingShelfQuery shelfQuery;
    private readonly ReadingShelfCommands shelfCommands;
    private readonly ReadingHistoryQuery history;
    private readonly ReadingProgressUseCases progress;
    private readonly ReaderPreferenceUseCases preferences;

    public ReadingStateService(
        IReadingStateRepository repository,
        ICanonicalBookRepository books,
        IContentPolicyReader contentPolicy,
        TimeProvider clock)
    {
        var visibleBooks = new ReadingBookVisibility(books, contentPolicy);
        shelfQuery = new(repository, books, visibleBooks);
        shelfCommands = new(repository, visibleBooks, shelfQuery, clock);
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
        => shelfQuery.ListAsync(userId, BoundLimit(limit), cancellationToken);

    public Task<ReadingOperationResult<ReadingShelfItem>> PutShelfAsync(
        Guid userId,
        Guid canonicalBookId,
        ShelfStatus status,
        CancellationToken cancellationToken = default)
        => shelfCommands.PutAsync(userId, canonicalBookId, status, cancellationToken);

    public Task<ReadingResultStatus> RemoveShelfAsync(
        Guid userId,
        Guid canonicalBookId,
        CancellationToken cancellationToken = default)
        => shelfCommands.RemoveAsync(userId, canonicalBookId, cancellationToken);

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
