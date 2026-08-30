using InkFlow.BuildingBlocks.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace InkFlow.Worker;

/// <summary>
/// Worker 中的 Inbox 消费循环：只领取已注册 Handler 类型，处理成功后确认。
/// 没有 Handler 时安全退出，不把未知消息变成无界失败重试。
/// </summary>
internal sealed class InboxConsumerBackgroundService(
    IServiceScopeFactory scopeFactory,
    InboxConsumerOptions options,
    ILogger<InboxConsumerBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Enabled)
        {
            logger.LogInformation("inbox consumer is disabled by configuration.");
            return;
        }

        using (var initialScope = scopeFactory.CreateScope())
        {
            var resolver = initialScope.ServiceProvider
                .GetRequiredService<IIntegrationMessageHandlerResolver>();
            if (resolver.MessageTypes.Count == 0)
            {
                logger.LogInformation(
                    "inbox consumer is idle because no integration message handlers are registered.");
                return;
            }
        }

        await Task.Delay(options.StartupDelay, stoppingToken).ConfigureAwait(false);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var pump = scope.ServiceProvider
                    .GetRequiredService<IInboxConsumerPump>();
                var result = await pump
                    .ConsumeOnceAsync(stoppingToken)
                    .ConfigureAwait(false);

                if (result.ClaimedCount > 0)
                {
                    if (result.DeadLetteredCount > 0)
                    {
                        logger.LogWarning(
                            "inbox consumer dead-lettered messages in a batch: claimed={ClaimedCount}, processed={ProcessedCount}, failed={FailedCount}, skipped={SkippedCount}, deadLettered={DeadLetteredCount}.",
                            result.ClaimedCount,
                            result.ProcessedCount,
                            result.FailedCount,
                            result.SkippedCount,
                            result.DeadLetteredCount);
                    }
                    else
                    {
                        logger.LogInformation(
                            "inbox consumer completed a batch: claimed={ClaimedCount}, processed={ProcessedCount}, failed={FailedCount}, skipped={SkippedCount}, deadLettered={DeadLetteredCount}.",
                            result.ClaimedCount,
                            result.ProcessedCount,
                            result.FailedCount,
                            result.SkippedCount,
                            result.DeadLetteredCount);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    "inbox consumer failed with {FailureType}; the next poll will retry leased messages.",
                    exception.GetType().Name);
            }

            await Task.Delay(options.PollInterval, stoppingToken).ConfigureAwait(false);
        }
    }
}
