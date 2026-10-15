using InkFlow.BuildingBlocks.Messaging;

namespace InkFlow.Worker;

/// <summary>
/// 周期性删除已成功处理且超过保留期的消息；失败/待重试消息不会被清理。
/// </summary>
internal sealed class MessageRetentionBackgroundService(
    IServiceScopeFactory scopeFactory,
    MessageRetentionOptions options,
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
                    .GetRequiredService<IMessageRetentionService>();
                var result = await retention
                    .CleanupAsync(options, stoppingToken)
                    .ConfigureAwait(false);
                Console.WriteLine(
                    $"message retention cleanup at {clock.GetUtcNow():O}: " +
                    $"outbox={result.OutboxDeletedCount}, inbox={result.InboxDeletedCount}.");
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                Console.WriteLine(
                    $"message retention cleanup failed: {exception.GetType().Name}.");
            }

            await Task.Delay(Interval, stoppingToken).ConfigureAwait(false);
        }
    }
}
