using Microsoft.Extensions.Logging;

namespace InkFlow.BuildingBlocks.Observability;

/// <summary>
/// 失败观测的编排模块。单个 sink 故障不会改变任务的重试/死信结果，
/// 保持观测路径对任务事实状态的非阻塞语义。
/// </summary>
public sealed class CrawlerFailureReporter(
    IEnumerable<ICrawlerFailureSink> sinks,
    ILogger<CrawlerFailureReporter> logger)
{
    private readonly IReadOnlyList<ICrawlerFailureSink> _sinks = sinks.ToList();

    public void Report(CrawlerFailureObservation observation)
    {
        foreach (var sink in _sinks)
        {
            try
            {
                sink.Record(observation);
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "crawler failure sink {SinkType} failed for task {TaskId}",
                    sink.GetType().Name,
                    observation.TaskId);
            }
        }
    }
}
