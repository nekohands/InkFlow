using InkFlow.Modules.Library.Domain;
using InkFlow.Modules.Sources.Application;

namespace InkFlow.Modules.Library.Application;

public sealed record MatchOutcome(
    bool IsSuccess,
    CanonicalBook? Book,
    bool NewlyCreated,
    IReadOnlyList<string> Errors)
{
    public static MatchOutcome Ok(CanonicalBook book, bool newlyCreated) =>
        new(true, book, newlyCreated, []);
}

/// <summary>
/// 来源书 → 正典书的匹配入口（Library 拥有匹配所有权）。
/// v1 匹配策略：
/// 1. 已有 Confirmed 候选 → 幂等返回既有正典书；
/// 2. 书名+作者归一化命中既有正典书（同书自动挂接,双源场景核心）→ 新建 Confirmed 候选指向该书；
/// 3. 均未命中 → 以来源元数据创建新正典书 + Confirmed 候选。
/// 多证据评分与人工审核属于 Phase 2 / 审核流程，不在本服务范围内。
/// </summary>
public sealed class CanonicalBookMatchingService(
    ISourceBookRepository sourceBookRepository,
    ICanonicalBookRepository canonicalBookRepository,
    IMatchCandidateRepository matchCandidateRepository)
{
    private static readonly TimeProvider Clock = TimeProvider.System;

    public async Task<MatchOutcome> CreateOrMatchAsync(
        string sourceId, string externalBookId, CancellationToken cancellationToken = default)
    {
        // 快路径：既有 Confirmed 候选是不可变事实，幂等返回且无需互斥。
        var existing = await matchCandidateRepository
            .FindForSourceBookAsync(sourceId, externalBookId, cancellationToken)
            .ConfigureAwait(false);

        if (existing is { Status: MatchCandidateStatus.Confirmed })
        {
            return await ResolveConfirmedAsync(existing, cancellationToken).ConfigureAwait(false);
        }

        var sourceMetadata = await sourceBookRepository
            .GetMetadataAsync(sourceId, externalBookId, cancellationToken)
            .ConfigureAwait(false);

        if (sourceMetadata is null)
        {
            return new MatchOutcome(false, null, false,
            [
                $"match: source book '{sourceId}/{externalBookId}' does not exist; import it first.",
            ]);
        }

        // 临界区：并发匹配同一书身份（归一化同名同作者）时，检查-创建必须原子完成，
        // 否则会为同一本书创建重复正典身份。作用域在单个事务内持有 (title, author)
        // 互斥锁；锁内复查候选（另一并发请求可能已完成整段流程）后再创建/挂接。
        await using var scope = await canonicalBookRepository
            .BeginTitleAuthorScopeAsync(sourceMetadata.Title, sourceMetadata.Author, cancellationToken)
            .ConfigureAwait(false);

        var raced = await matchCandidateRepository
            .FindForSourceBookAsync(sourceId, externalBookId, cancellationToken)
            .ConfigureAwait(false);
        if (raced is { Status: MatchCandidateStatus.Confirmed })
        {
            await scope.CommitAsync(cancellationToken).ConfigureAwait(false);
            return await ResolveConfirmedAsync(raced, cancellationToken).ConfigureAwait(false);
        }

        // 同书自动挂接:另一来源已导入的同名同作者书 → 复用其正典书(BookId 不变)。
        var sameCanonical = await canonicalBookRepository
            .FindByTitleAuthorAsync(sourceMetadata.Title, sourceMetadata.Author, cancellationToken)
            .ConfigureAwait(false);

        CanonicalBook book;
        var newlyCreated = false;

        if (sameCanonical is not null)
        {
            book = sameCanonical;
        }
        else
        {
            book = CanonicalBook.Create(sourceMetadata.Title, sourceMetadata.Author, Clock.GetUtcNow());
            await canonicalBookRepository.AddAsync(book, cancellationToken).ConfigureAwait(false);
            newlyCreated = true;
        }

        var newCandidate = MatchCandidate.Confirm(
            book.Id, sourceId, externalBookId, Clock.GetUtcNow());
        await matchCandidateRepository.AddAsync(newCandidate, cancellationToken).ConfigureAwait(false);

        await scope.CommitAsync(cancellationToken).ConfigureAwait(false);
        return MatchOutcome.Ok(book, newlyCreated);
    }

    private async Task<MatchOutcome> ResolveConfirmedAsync(
        MatchCandidate confirmedCandidate, CancellationToken cancellationToken)
    {
        var confirmed = await canonicalBookRepository
            .GetAsync(confirmedCandidate.CanonicalBookId, cancellationToken)
            .ConfigureAwait(false);

        return confirmed is null
            ? new MatchOutcome(false, null, false,
                [$"match: candidate {confirmedCandidate.Id} points to missing book {confirmedCandidate.CanonicalBookId}."])
            : MatchOutcome.Ok(confirmed, newlyCreated: false);
    }
}
