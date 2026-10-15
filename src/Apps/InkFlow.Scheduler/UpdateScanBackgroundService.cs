using InkFlow.Modules.Crawling.Application;
using InkFlow.Modules.Sources.Application;

namespace InkFlow.Scheduler;

/// <summary>
/// 追更扫描后台服务:按固定间隔为已导入书目入队 Toc 同步任务(带活跃任务去重)。
/// Worker 消费任务完成"检测新章 → 抓取 → 落库 → 映射",全程无需人工干预。
/// </summary>
internal sealed class UpdateScanBackgroundService(
    IServiceScopeFactory scopeFactory) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(30);
    private SourceBookScanCursor? _cursor;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // 启动后先等一个间隔,避免与迁移/其他服务抢启动窗口。
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var scanner = ActivatorUtilities.CreateInstance<UpdateScanService>(scope.ServiceProvider);
                var batch = await scanner
                    .EnqueueTocScanBatchAsync(_cursor, stoppingToken)
                    .ConfigureAwait(false);
                _cursor = batch.HasMore ? batch.NextCursor : null;
                Console.WriteLine(
                    $"update scan enqueued {batch.EnqueuedCount} toc task(s); " +
                    $"scanned {batch.ScannedCount} book(s), hasMore={batch.HasMore}.");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Console.WriteLine("update scan failed.");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }
}
