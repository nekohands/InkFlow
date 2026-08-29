using InkFlow.BuildingBlocks.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace InkFlow.Worker;

/// <summary>
/// Worker 中的 Outbox relay：按固定间隔领取并投递一批消息，失败交由 Dispatcher 的
/// lease/退避语义处理。这里不记录载荷、异常文本或消息内容，避免后台日志携带业务数据。
/// </summary>
internal sealed class OutboxRelayBackgroundService(
    IServiceScopeFactory scopeFactory,
    OutboxRelayOptions options,
    ILogger<OutboxRelayBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Enabled)
        {
            logger.LogInformation("outbox relay is disabled by configuration.");
            return;
        }

        await Task.Delay(options.StartupDelay, stoppingToken).ConfigureAwait(false);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var dispatcher = scope.ServiceProvider
                    .GetRequiredService<IOutboxDispatcher>();
                var result = await dispatcher
                    .DispatchOnceAsync(stoppingToken)
                    .ConfigureAwait(false);

                if (result.ClaimedCount > 0)
                {
                    logger.LogInformation(
                        "outbox relay completed a batch: claimed={ClaimedCount}, published={PublishedCount}, failed={FailedCount}.",
                        result.ClaimedCount,
                        result.PublishedCount,
                        result.FailedCount);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    "outbox relay failed with {FailureType}; the next poll will retry leased messages.",
                    exception.GetType().Name);
            }

            await Task.Delay(options.PollInterval, stoppingToken).ConfigureAwait(false);
        }
    }
}
