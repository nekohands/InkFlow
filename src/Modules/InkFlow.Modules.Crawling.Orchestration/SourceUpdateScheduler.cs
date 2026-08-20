using InkFlow.BuildingBlocks.Persistence;
using InkFlow.Modules.Crawling;
using InkFlow.Modules.Sources.Rules;
using Microsoft.EntityFrameworkCore;

namespace InkFlow.Modules.Crawling.Orchestration;

public sealed record SourceUpdateScheduleResult(int Considered, int Enqueued);

public sealed class SourceUpdateScheduler(SourcesDbContext sources, CrawlingDbContext crawling)
{
    private readonly CrawlerTaskStore _tasks = new(crawling);

    public async Task<SourceUpdateScheduleResult> ScheduleDueAsync(
        DateTimeOffset now,
        TimeSpan updateInterval,
        int batchSize = 500,
        CancellationToken cancellationToken = default)
    {
        if (updateInterval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(updateInterval));
        }
        if (batchSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(batchSize));
        }

        var dueBefore = now.Subtract(updateInterval);
        var books = await sources.SourceBooks.AsNoTracking()
            .Where(book => book.LastCheckedAtUtc == null || book.LastCheckedAtUtc <= dueBefore)
            .OrderBy(book => book.LastCheckedAtUtc)
            .ThenBy(book => book.CreatedAtUtc)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        if (books.Count == 0)
        {
            return new SourceUpdateScheduleResult(0, 0);
        }

        var sourceIds = books.Select(book => book.SourceId).Distinct().ToArray();
        var sourceRecords = await sources.Sources.AsNoTracking()
            .Where(source => sourceIds.Contains(source.Id) && source.Status == "Active" && source.HealthScore >= 40)
            .ToDictionaryAsync(source => source.Id, cancellationToken);
        var publishedRules = await sources.RuleVersions.AsNoTracking()
            .Where(rule => sourceIds.Contains(rule.SourceId) && rule.Status == "Published")
            .OrderByDescending(rule => rule.Version)
            .ToListAsync(cancellationToken);

        var enqueued = 0;
        var slotSeconds = Math.Max(1L, (long)Math.Ceiling(updateInterval.TotalSeconds));
        var slot = now.ToUnixTimeSeconds() / slotSeconds;

        foreach (var book in books)
        {
            if (!sourceRecords.TryGetValue(book.SourceId, out var source))
            {
                continue;
            }

            var rule = source.ActiveRuleVersionId.HasValue
                ? publishedRules.FirstOrDefault(candidate => candidate.SourceId == source.Id && candidate.Id == source.ActiveRuleVersionId.Value)
                : publishedRules.FirstOrDefault(candidate => candidate.SourceId == source.Id);
            if (rule is null)
            {
                continue;
            }

            var payload = new RuleCrawlerTaskPayload(
                source.Id,
                rule.Id,
                SourceOperation.Toc,
                book.Id,
                null,
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["bookUrl"] = book.Url,
                    ["externalId"] = book.ExternalId
                });

            var task = new CrawlerTaskRecord
            {
                Id = Guid.CreateVersion7(),
                Type = RuleCrawlerTaskPayload.TaskType,
                SourceId = source.Id,
                Payload = payload.Serialize(),
                IdempotencyKey = $"update:{book.Id}:{rule.Id}:{slot}",
                Priority = 70,
                MaxAttempts = 5,
                ScheduledAtUtc = now,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
                TraceId = Guid.CreateVersion7().ToString("N")
            };

            if (await _tasks.TryEnqueueAsync(task, cancellationToken))
            {
                enqueued++;
            }
        }

        return new SourceUpdateScheduleResult(books.Count, enqueued);
    }
}
