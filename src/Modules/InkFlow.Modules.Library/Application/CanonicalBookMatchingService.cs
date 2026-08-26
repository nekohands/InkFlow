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
/// v1 策略：按来源外部身份精确匹配——
/// 1. 已有 Confirmed 候选 → 幂等返回既有正典书；
/// 2. 无候选 → 以来源元数据创建新正典书 + Confirmed 候选。
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

        var now = Clock.GetUtcNow();
        var book = CanonicalBook.Create(sourceBook.Title, sourceBook.Author, now);
        await canonicalBookRepository.AddAsync(book, cancellationToken).ConfigureAwait(false);

        var candidate = MatchCandidate.Confirm(book.Id, sourceId, externalBookId, now);
        await matchCandidateRepository.AddAsync(candidate, cancellationToken).ConfigureAwait(false);

        return MatchOutcome.Ok(book, newlyCreated: true);
    }
}
