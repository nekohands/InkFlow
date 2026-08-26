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
        var existing = await matchCandidateRepository
            .FindForSourceBookAsync(sourceId, externalBookId, cancellationToken)
            .ConfigureAwait(false);

        if (existing is { Status: MatchCandidateStatus.Confirmed })
        {
            var confirmed = await canonicalBookRepository
                .GetAsync(existing.CanonicalBookId, cancellationToken)
                .ConfigureAwait(false);

            return confirmed is null
                ? new MatchOutcome(false, null, false,
                    [$"match: candidate {existing.Id} points to missing book {existing.CanonicalBookId}."])
                : MatchOutcome.Ok(confirmed, newlyCreated: false);
        }

        var sourceBook = await sourceBookRepository
            .GetAsync(sourceId, externalBookId, cancellationToken)
            .ConfigureAwait(false);

        if (sourceBook is null)
        {
            return new MatchOutcome(false, null, false,
            [
                $"match: source book '{sourceId}/{externalBookId}' does not exist; import it first.",
            ]);
        }

        // 同书自动挂接:另一来源已导入的同名同作者书 → 复用其正典书(BookId 不变)。
        var sameCanonical = await canonicalBookRepository
            .FindByTitleAuthorAsync(sourceBook.Title, sourceBook.Author, cancellationToken)
            .ConfigureAwait(false);

        CanonicalBook book;
        var newlyCreated = false;

        if (sameCanonical is not null)
        {
            book = sameCanonical;
        }
        else
        {
            book = CanonicalBook.Create(sourceBook.Title, sourceBook.Author, Clock.GetUtcNow());
            await canonicalBookRepository.AddAsync(book, cancellationToken).ConfigureAwait(false);
            newlyCreated = true;
        }

        var newCandidate = MatchCandidate.Confirm(
            book.Id, sourceId, externalBookId, Clock.GetUtcNow());
        await matchCandidateRepository.AddAsync(newCandidate, cancellationToken).ConfigureAwait(false);

        return MatchOutcome.Ok(book, newlyCreated);
    }
}
