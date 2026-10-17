using InkFlow.Modules.Reading.Domain;

namespace InkFlow.Modules.Reading.Application;

/// <summary>书架写入用例：负责可见性校验、状态变更和幂等删除。</summary>
internal sealed class ReadingShelfCommands(
    IReadingStateRepository repository,
    ReadingBookVisibility visibleBooks,
    ReadingShelfQuery shelf,
    TimeProvider clock)
{
    public async Task<ReadingOperationResult<ReadingShelfItem>> PutAsync(
        Guid userId,
        Guid canonicalBookId,
        ShelfStatus status,
        CancellationToken cancellationToken)
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

    public async Task<ReadingResultStatus> RemoveAsync(
        Guid userId,
        Guid canonicalBookId,
        CancellationToken cancellationToken)
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
}
