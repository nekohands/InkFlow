using InkFlow.Modules.Crawling.Application;

namespace InkFlow.Worker;

/// <summary>
/// 轮询消费:领取可执行任务 → 执行 → 完成/失败落库。v1 串行消费,批量并发属后续优化。
/// </summary>
internal sealed class TaskPollingService(
    IServiceScopeFactory scopeFactory,
    TimeProvider clock) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var tasks = scope.ServiceProvider.GetRequiredService<ICrawlerTaskRepository>();

                var task = await tasks.TryLeaseAsync(
                        clock.GetUtcNow(),
                        CrawlerTaskExecutionDefaults.Owner,
                        CrawlerTaskExecutionDefaults.LeaseDuration,
                        stoppingToken)
                    .ConfigureAwait(false);
                if (task is not null)
                {
                    var processor = scope.ServiceProvider
                        .GetRequiredService<ICrawlerTaskProcessor>();
                    await processor
                        .ProcessAsync(task, stoppingToken)
                        .ConfigureAwait(false);

                    // 追更联动一次可能入队整批正文任务;有活时短轮询尽快消化,
                    // 空闲时才回到低成本长等待。
                    await Task.Delay(TimeSpan.FromMilliseconds(250), stoppingToken)
                        .ConfigureAwait(false);
                    continue;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Console.WriteLine("polling failed.");
            }

            await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
        }
    }
}
