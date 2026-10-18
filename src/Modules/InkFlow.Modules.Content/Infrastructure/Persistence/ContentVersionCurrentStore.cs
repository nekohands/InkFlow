using Microsoft.EntityFrameworkCore;

namespace InkFlow.Modules.Content.Infrastructure.Persistence;

internal sealed class ContentVersionCurrentStore(ContentDbContext db)
{
    /// <summary>在同一事务内原子切换当前版本，并拒绝跨章节版本。</summary>
    public async Task SetCurrentAsync(
        Guid chapterId, Guid versionId, CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);

        var targetExists = await db.Versions
            .AnyAsync(
                v => v.Id == versionId && v.CanonicalChapterId == chapterId,
                cancellationToken)
            .ConfigureAwait(false);
        if (!targetExists)
        {
            throw new InvalidOperationException(
                $"content version '{versionId}' does not belong to chapter '{chapterId}'.");
        }

        // 单条 UPDATE 同时把同章节其它版本置为 false，并将目标置为 true，
        // 避免两条语句之间暴露“没有当前版本”的中间状态。
        await db.Versions
            .Where(v => v.CanonicalChapterId == chapterId)
            .ExecuteUpdateAsync(
                s => s.SetProperty(v => v.IsCurrent, v => v.Id == versionId),
                cancellationToken)
            .ConfigureAwait(false);

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }
}
