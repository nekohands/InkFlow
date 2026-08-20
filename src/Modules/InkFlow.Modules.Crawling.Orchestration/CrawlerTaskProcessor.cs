using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using InkFlow.BuildingBlocks.Persistence;
using InkFlow.Modules.Content;
using InkFlow.Modules.Crawling;
using InkFlow.Modules.Library;
using InkFlow.Modules.Sources.Rules;
using Microsoft.EntityFrameworkCore;

namespace InkFlow.Modules.Crawling.Orchestration;

public sealed class CrawlerTaskProcessor(
    CrawlingDbContext crawling,
    SourcesDbContext sources,
    LibraryDbContext library,
    ContentDbContext content,
    TimeProvider timeProvider)
{
    private readonly CrawlerTaskStore _tasks = new(crawling);

    public async Task<bool> ProcessOneAsync(string workerId, CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();
        var task = await _tasks.LeaseNextAsync(workerId, now, TimeSpan.FromMinutes(2), cancellationToken);
        if (task is null)
        {
            return false;
        }

        try
        {
            if (!string.Equals(task.Type, RuleCrawlerTaskPayload.TaskType, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Unsupported crawler task type {task.Type}.");
            }

            var payload = RuleCrawlerTaskPayload.Deserialize(task.Payload);
            var ruleVersion = await sources.RuleVersions.AsNoTracking().SingleAsync(rule => rule.Id == payload.RuleVersionId && rule.SourceId == payload.SourceId, cancellationToken);
            var rule = SourceRuleJson.Deserialize(ruleVersion.RuleJson);
            var execution = await new RuleOperationExecutor().ExecuteAsync(rule, payload.Operation, payload.Variables, cancellationToken);
            await ApplyExecutionAsync(task.Id, payload, execution, now, cancellationToken);
            await _tasks.MarkCompletedAsync(task.Id, workerId, timeProvider.GetUtcNow(), cancellationToken);
            return true;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            await _tasks.MarkFailedAsync(task.Id, workerId, timeProvider.GetUtcNow(), exception.Message, TimeSpan.FromSeconds(5), cancellationToken);
            return true;
        }
    }

    public async Task ApplyExecutionAsync(
        Guid? crawlerTaskId,
        RuleCrawlerTaskPayload payload,
        SourceOperationExecution execution,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        crawling.FetchArtifacts.Add(new FetchArtifactRecord
        {
            Id = Guid.CreateVersion7(),
            CrawlerTaskId = crawlerTaskId,
            SourceId = payload.SourceId,
            SourceChapterId = payload.SourceChapterId,
            RuleVersionId = payload.RuleVersionId,
            Url = execution.FinalUri.AbsoluteUri,
            StatusCode = execution.StatusCode,
            RawHash = ContentNormalizer.RawHash(execution.RawContent),
            RawBody = execution.RawContent,
            FetchedAtUtc = now
        });
        await crawling.SaveChangesAsync(cancellationToken);

        switch (payload.Operation)
        {
            case SourceOperation.BookInfo:
                await ApplyBookInfoAsync(payload, execution, now, cancellationToken);
                break;
            case SourceOperation.Toc:
            case SourceOperation.Update:
                await ApplyTocAsync(payload, execution, now, cancellationToken);
                break;
            case SourceOperation.Content:
                await ApplyContentAsync(payload, execution, now, cancellationToken);
                break;
            case SourceOperation.Search:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(payload), payload.Operation, null);
        }
    }

    private async Task ApplyBookInfoAsync(RuleCrawlerTaskPayload payload, SourceOperationExecution execution, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var row = execution.Extraction.Rows.FirstOrDefault() ?? throw new InvalidOperationException("BookInfo returned no row.");
        var externalId = Required(row, "externalId", payload.Variables.GetValueOrDefault("externalId"));
        var title = Required(row, "title", row.GetValueOrDefault("name"));
        var author = row.GetValueOrDefault("author") ?? string.Empty;
        var url = row.GetValueOrDefault("url") ?? payload.Variables.GetValueOrDefault("bookUrl") ?? execution.FinalUri.AbsoluteUri;

        var sourceBook = await sources.SourceBooks.SingleOrDefaultAsync(book => book.SourceId == payload.SourceId && book.ExternalId == externalId, cancellationToken);
        if (sourceBook is null)
        {
            sourceBook = new SourceBookRecord { Id = Guid.CreateVersion7(), SourceId = payload.SourceId, ExternalId = externalId, CreatedAtUtc = now };
            sources.SourceBooks.Add(sourceBook);
        }
        sourceBook.Url = url;
        sourceBook.Title = title;
        sourceBook.Author = author;
        sourceBook.Description = row.GetValueOrDefault("description") ?? row.GetValueOrDefault("intro");
        sourceBook.UpdatedAtUtc = now;
        await sources.SaveChangesAsync(cancellationToken);

        var normalizedTitle = TextIdentityNormalizer.Normalize(title);
        var normalizedAuthor = TextIdentityNormalizer.Normalize(author);
        var candidates = await library.Books.Where(book => book.NormalizedTitle == normalizedTitle).Take(20).ToListAsync(cancellationToken);
        var best = candidates.Select(book => (Book: book, Match: BookMatchEngine.Evaluate(title, author, book.Title, book.Author)))
            .OrderByDescending(item => item.Match.Score).FirstOrDefault();

        BookRecord canonical;
        BookMatchResult matchResult;
        if (best.Book is not null && best.Match.AutoMatch)
        {
            canonical = best.Book;
            matchResult = best.Match;
        }
        else
        {
            canonical = new BookRecord
            {
                Id = Guid.CreateVersion7(), Title = title, NormalizedTitle = normalizedTitle,
                Author = author, NormalizedAuthor = normalizedAuthor, Description = sourceBook.Description,
                CreatedAtUtc = now, UpdatedAtUtc = now
            };
            library.Books.Add(canonical);
            matchResult = new BookMatchResult(100, [new MatchEvidence("CreatedCanonical", 100, "No trusted existing canonical match was found.")]);
        }

        var existingMatch = await library.SourceBookMatches.SingleOrDefaultAsync(item => item.SourceBookId == sourceBook.Id, cancellationToken);
        if (existingMatch is null)
        {
            library.SourceBookMatches.Add(new SourceBookMatchRecord
            {
                Id = Guid.CreateVersion7(), BookId = canonical.Id, SourceBookId = sourceBook.Id,
                Score = matchResult.Score, EvidenceJson = JsonSerializer.Serialize(matchResult.Evidence),
                AlgorithmVersion = BookMatchEngine.AlgorithmVersion, CreatedAtUtc = now
            });
        }
        await library.SaveChangesAsync(cancellationToken);

        await EnqueueAsync(new RuleCrawlerTaskPayload(
            payload.SourceId, payload.RuleVersionId, SourceOperation.Toc, sourceBook.Id, null,
            new Dictionary<string, string>(payload.Variables, StringComparer.OrdinalIgnoreCase)
            {
                ["bookUrl"] = sourceBook.Url,
                ["externalId"] = sourceBook.ExternalId
            }), $"toc:{sourceBook.Id}:{payload.RuleVersionId}:initial", 80, now, cancellationToken);
    }

    private async Task ApplyTocAsync(RuleCrawlerTaskPayload payload, SourceOperationExecution execution, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (!payload.SourceBookId.HasValue)
        {
            throw new InvalidOperationException("TOC task requires SourceBookId.");
        }
        var sourceBook = await sources.SourceBooks.SingleAsync(book => book.Id == payload.SourceBookId.Value, cancellationToken);
        var bookMatch = await library.SourceBookMatches.AsNoTracking().SingleAsync(match => match.SourceBookId == sourceBook.Id, cancellationToken);
        var canonicalChapters = await library.Chapters.Where(chapter => chapter.BookId == bookMatch.BookId).ToListAsync(cancellationToken);
        var anyNew = false;
        var index = 0;

        foreach (var row in execution.Extraction.Rows)
        {
            index++;
            var title = Required(row, "title", row.GetValueOrDefault("name"));
            var url = Required(row, "url", null);
            var externalId = row.GetValueOrDefault("externalId") ?? url;
            var sequence = index * 100000L;
            var sourceChapter = await sources.SourceChapters.SingleOrDefaultAsync(chapter => chapter.SourceBookId == sourceBook.Id && chapter.ExternalId == externalId, cancellationToken);
            var isNew = sourceChapter is null;
            if (sourceChapter is null)
            {
                sourceChapter = new SourceChapterRecord { Id = Guid.CreateVersion7(), SourceBookId = sourceBook.Id, ExternalId = externalId, CreatedAtUtc = now };
                sources.SourceChapters.Add(sourceChapter);
                anyNew = true;
            }
            var changed = sourceChapter.Title != title || sourceChapter.Url != url;
            sourceChapter.Title = title;
            sourceChapter.Url = url;
            sourceChapter.Sequence = sequence;
            sourceChapter.UpdatedAtUtc = now;
            await sources.SaveChangesAsync(cancellationToken);

            var mapped = await library.ChapterMappings.AsNoTracking().AnyAsync(mapping => mapping.SourceChapterId == sourceChapter.Id, cancellationToken);
            if (!mapped)
            {
                var number = ChapterNumberParser.Parse(title);
                var candidate = number.HasValue ? canonicalChapters.FirstOrDefault(chapter => chapter.DisplayNumber == number) : null;
                candidate ??= canonicalChapters.FirstOrDefault(chapter => chapter.Sequence == sequence);
                ChapterAlignmentResult alignment;
                if (candidate is null)
                {
                    candidate = new ChapterRecord
                    {
                        Id = Guid.CreateVersion7(), BookId = bookMatch.BookId, Sequence = sequence, DisplayNumber = number,
                        Title = title, NormalizedTitle = TextIdentityNormalizer.Normalize(title), CreatedAtUtc = now, UpdatedAtUtc = now
                    };
                    library.Chapters.Add(candidate);
                    canonicalChapters.Add(candidate);
                    alignment = new ChapterAlignmentResult(100, [new MatchEvidence("CreatedCanonicalChapter", 100, "No canonical chapter candidate existed.")]);
                }
                else
                {
                    alignment = ChapterAlignmentEngine.Evaluate(new ChapterIdentity(title, sequence, number), new ChapterIdentity(candidate.Title, candidate.Sequence, candidate.DisplayNumber));
                }

                library.ChapterMappings.Add(new ChapterMappingRecord
                {
                    Id = Guid.CreateVersion7(), ChapterId = candidate.Id, SourceChapterId = sourceChapter.Id,
                    Score = alignment.Score, EvidenceJson = JsonSerializer.Serialize(alignment.Evidence),
                    AlgorithmVersion = ChapterAlignmentEngine.AlgorithmVersion, CreatedAtUtc = now
                });
                await library.SaveChangesAsync(cancellationToken);
            }

            if (isNew || changed)
            {
                var token = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{url}|{title}"))).ToLowerInvariant()[..16];
                await EnqueueAsync(new RuleCrawlerTaskPayload(
                    payload.SourceId, payload.RuleVersionId, SourceOperation.Content, sourceBook.Id, sourceChapter.Id,
                    new Dictionary<string, string>(payload.Variables, StringComparer.OrdinalIgnoreCase)
                    {
                        ["bookUrl"] = sourceBook.Url,
                        ["chapterUrl"] = sourceChapter.Url,
                        ["externalId"] = sourceChapter.ExternalId
                    }), $"content:{sourceChapter.Id}:{token}", 100, now, cancellationToken);
            }
        }

        sourceBook.LastCheckedAtUtc = now;
        if (anyNew)
        {
            sourceBook.LastUpdatedAtUtc = now;
        }
        await sources.SaveChangesAsync(cancellationToken);
    }

    private async Task ApplyContentAsync(RuleCrawlerTaskPayload payload, SourceOperationExecution execution, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (!payload.SourceChapterId.HasValue)
        {
            throw new InvalidOperationException("Content task requires SourceChapterId.");
        }
        var row = execution.Extraction.Rows.FirstOrDefault() ?? throw new InvalidOperationException("Content operation returned no row.");
        var body = Required(row, "content", null);
        var mapping = await library.ChapterMappings.AsNoTracking().Where(item => item.SourceChapterId == payload.SourceChapterId.Value)
            .OrderByDescending(item => item.Score).FirstAsync(cancellationToken);
        var source = await sources.Sources.AsNoTracking().SingleAsync(item => item.Id == payload.SourceId, cancellationToken);
        var normalized = ContentNormalizer.FromPlainText(body);
        var quality = ContentQualityEngine.Evaluate(normalized.Document, source.HealthScore);

        var blob = await content.ContentBlobs.SingleOrDefaultAsync(item => item.ContentHash == normalized.CanonicalHash, cancellationToken);
        if (blob is null)
        {
            blob = new ContentBlobRecord
            {
                Id = Guid.CreateVersion7(), ContentHash = normalized.CanonicalHash, InlineContent = normalized.CanonicalText,
                SizeBytes = Encoding.UTF8.GetByteCount(normalized.CanonicalText), CreatedAtUtc = now
            };
            content.ContentBlobs.Add(blob);
        }

        var version = await content.ContentVersions.SingleOrDefaultAsync(item => item.SourceChapterId == payload.SourceChapterId.Value && item.CanonicalHash == normalized.CanonicalHash, cancellationToken);
        if (version is null)
        {
            version = new ContentVersionRecord
            {
                Id = Guid.CreateVersion7(), ChapterId = mapping.ChapterId, SourceChapterId = payload.SourceChapterId.Value,
                BlobId = blob.Id, RawHash = ContentNormalizer.RawHash(execution.RawContent), CanonicalHash = normalized.CanonicalHash,
                QualityScore = quality.Score, EvidenceJson = JsonSerializer.Serialize(quality.Evidence), NormalizerVersion = ContentNormalizer.Version, CreatedAtUtc = now
            };
            content.ContentVersions.Add(version);
        }
        await content.SaveChangesAsync(cancellationToken);

        var selection = await content.ChapterSelections.SingleOrDefaultAsync(item => item.ChapterId == mapping.ChapterId, cancellationToken);
        if (selection is null)
        {
            content.ChapterSelections.Add(new ChapterSelectionRecord { ChapterId = mapping.ChapterId, ContentVersionId = version.Id, SelectedAtUtc = now });
        }
        else if (!selection.IsLocked)
        {
            var current = await content.ContentVersions.AsNoTracking().SingleOrDefaultAsync(item => item.Id == selection.ContentVersionId, cancellationToken);
            if (current is null || version.QualityScore > current.QualityScore)
            {
                selection.ContentVersionId = version.Id;
                selection.SelectedAtUtc = now;
                selection.Reason = "quality-engine";
            }
        }
        await content.SaveChangesAsync(cancellationToken);
    }

    private async Task EnqueueAsync(RuleCrawlerTaskPayload payload, string idempotencyKey, int priority, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (await crawling.CrawlerTasks.AsNoTracking().AnyAsync(task => task.IdempotencyKey == idempotencyKey, cancellationToken))
        {
            return;
        }
        await _tasks.EnqueueAsync(new CrawlerTaskRecord
        {
            Id = Guid.CreateVersion7(), Type = RuleCrawlerTaskPayload.TaskType, SourceId = payload.SourceId,
            Payload = payload.Serialize(), IdempotencyKey = idempotencyKey, Priority = priority,
            MaxAttempts = 5, ScheduledAtUtc = now, CreatedAtUtc = now, UpdatedAtUtc = now
        }, cancellationToken);
    }

    private static string Required(IReadOnlyDictionary<string, string> row, string key, string? fallback)
    {
        if (row.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
        {
            return value.Trim();
        }
        if (!string.IsNullOrWhiteSpace(fallback))
        {
            return fallback.Trim();
        }
        throw new InvalidOperationException($"Source operation did not provide required field '{key}'.");
    }
}
