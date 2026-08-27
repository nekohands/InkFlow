using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;

namespace InkFlow.BuildingBlocks.Observability;

/// <summary>抓取失败观测的适配器接口；日志和指标可独立替换或扩展。</summary>
public interface ICrawlerFailureSink
{
    void Record(CrawlerFailureObservation observation);
}

public static class CrawlerFailureMetrics
{
    public const string MeterName = "InkFlow.Crawler";
    public const string TaskFailuresName = "inkflow.crawler.task.failures";
    public const string DeadLettersName = "inkflow.crawler.task.dead_letters";
}

/// <summary>把任务失败发布为 OpenTelemetry counter；不使用 TaskId/原始原因作为标签。</summary>
public sealed class OpenTelemetryCrawlerFailureSink : ICrawlerFailureSink
{
    private static readonly Meter Meter = new(CrawlerFailureMetrics.MeterName, "1.0");
    private static readonly Counter<long> TaskFailures = Meter.CreateCounter<long>(
        CrawlerFailureMetrics.TaskFailuresName,
        unit: "{failure}",
        description: "Crawler task failures by capability, failure kind, and disposition.");
    private static readonly Counter<long> DeadLetters = Meter.CreateCounter<long>(
        CrawlerFailureMetrics.DeadLettersName,
        unit: "{dead_letter}",
        description: "Crawler tasks that exhausted retries and entered dead letter.");

    public void Record(CrawlerFailureObservation observation)
    {
        var tags = new TagList();
        tags.Add("crawler.capability", observation.Capability);
        tags.Add("crawler.failure.kind", observation.FailureKind);
        tags.Add("crawler.disposition", observation.Disposition);

        TaskFailures.Add(1, tags);
        if (observation.Disposition == CrawlerFailureDisposition.DeadLetter)
        {
            DeadLetters.Add(1, tags);
        }
    }
}

/// <summary>输出可检索的结构化失败日志；原始失败原因保留在死信记录中，不复制到日志。</summary>
public sealed class LoggingCrawlerFailureSink(
    ILogger<LoggingCrawlerFailureSink> logger) : ICrawlerFailureSink
{
    private static readonly EventId FailureEvent = new(2201, "CrawlerTaskFailure");

    public void Record(CrawlerFailureObservation observation)
    {
        var level = observation.Disposition == CrawlerFailureDisposition.DeadLetter
            ? LogLevel.Error
            : LogLevel.Warning;

        logger.Log(
            level,
            FailureEvent,
            "crawler task failure observed: taskId={TaskId} sourceId={SourceId} " +
            "capability={Capability} attempt={AttemptCount}/{MaxAttempts} " +
            "disposition={Disposition} failureKind={FailureKind} occurredAt={OccurredAt}",
            observation.TaskId,
            observation.SourceId,
            observation.Capability,
            observation.AttemptCount,
            observation.MaxAttempts,
            observation.Disposition,
            observation.FailureKind,
            observation.OccurredAt);
    }
}
