using InkFlow.Modules.Crawling.Domain;
using InkFlow.Modules.Sources.Domain;

namespace InkFlow.Modules.Crawling.Application;

internal enum ContentFetchEnqueueResult
{
    Skipped,
    Enqueued,
    RunStopped,
}

/// <summary>
/// Owns the atomic task boundary for content fetch candidates.
/// Normal scans and collection runs intentionally use different repository gates.
/// </summary>
internal sealed class ContentFetchTaskEnqueuer(
    ICrawlerTaskRepository taskRepository,
    CollectionRunService? collectionRuns = null)
{
    public async Task<ContentFetchEnqueueResult> TryEnqueueAsync(
        string sourceId,
        string externalBookId,
        ContentFetchCandidate candidate,
        DateTimeOffset createdAt,
        string? credentialReferenceId,
        Guid? runId,
        CancellationToken cancellationToken)
    {
        if (runId is { } activeRunId && collectionRuns is not null &&
            !await collectionRuns
                .CanScheduleFollowUpAsync(activeRunId, cancellationToken)
                .ConfigureAwait(false))
        {
            return ContentFetchEnqueueResult.RunStopped;
        }

        var task = CrawlerTask.Create(
            new CrawlPayload(
                sourceId,
                SourceCapability.Content,
                new Dictionary<string, string>
                {
                    ["bookId"] = externalBookId,
                    ["chapterId"] = candidate.ExternalChapterId,
                    ["reason"] = candidate.Reason,
                },
                credentialReferenceId,
                runId),
            createdAt: createdAt);

        var inserted = runId is { } runIdForEnqueue
            ? await taskRepository
                .TryAddIfNoConflictingTaskForCollectionRunAsync(
                    task,
                    runIdForEnqueue,
                    "chapterId",
                    candidate.ExternalChapterId,
                    cancellationToken,
                    ignoreDeadLettered: true)
                .ConfigureAwait(false)
            : await taskRepository
                .TryAddIfNoConflictingTaskAsync(
                    task,
                    "chapterId",
                    candidate.ExternalChapterId,
                    cancellationToken)
                .ConfigureAwait(false);

        return inserted
            ? ContentFetchEnqueueResult.Enqueued
            : ContentFetchEnqueueResult.Skipped;
    }
}
