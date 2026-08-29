using InkFlow.BuildingBlocks.Security;

/// <summary>周期性删除已超过保留期的审计事实；每轮执行由策略服务限制批次和总量。</summary>
internal sealed class AuditRetentionBackgroundService(
    IServiceScopeFactory scopeFactory,
    AuditRetentionOptions options,
    TimeProvider clock) : BackgroundService
{
    private static readonly TimeSpan InitialDelay = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(InitialDelay, stoppingToken).ConfigureAwait(false);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var retention = scope.ServiceProvider
                    .GetRequiredService<IAuditRetentionService>();
                var result = await retention
                    .CleanupAsync(options, stoppingToken)
                    .ConfigureAwait(false);
                Console.WriteLine(
                    $"audit retention cleanup at {clock.GetUtcNow():O}: " +
                    $"deleted={result.DeletedCount}.");
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                Console.WriteLine(
                    $"audit retention cleanup failed: {exception.GetType().Name}.");
            }

            await Task.Delay(Interval, stoppingToken).ConfigureAwait(false);
        }
    }
}
