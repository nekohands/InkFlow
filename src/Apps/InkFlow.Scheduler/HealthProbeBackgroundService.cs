using InkFlow.Modules.Sources.Application;

namespace InkFlow.Scheduler;

/// <summary>
/// 健康巡检后台服务:周期性对冷却期已满的 Unhealthy 能力主动发起轻量探针
/// (Search/Toc),成败经既有健康上报裁定,让无自然流量的来源也能自动恢复。
/// </summary>
internal sealed class HealthProbeBackgroundService(
    IServiceScopeFactory scopeFactory) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(10);
    private SourceHealthScanCursor? _cursor;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(45), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var prober = scope.ServiceProvider.GetRequiredService<HealthProbeService>();
                var batch = await prober
                    .ProbeDueBatchAsync(_cursor, stoppingToken)
                    .ConfigureAwait(false);
                foreach (var result in batch.Results)
                {
                    Console.WriteLine(
                        $"health probe {result.SourceId}/{result.Capability}: " +
                        (result.Recovered ? "recovered." : $"still failing ({result.FailureReason})."));
                }

                _cursor = batch.HasMore ? batch.NextCursor : null;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Console.WriteLine("health probe failed.");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }
}
