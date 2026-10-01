using InkFlow.Modules.Identity.Application;

/// <summary>周期性删除已过期且超过宽限期的 Identity 会话/令牌事实；批次与总量由选项限制。</summary>
internal sealed class IdentityRetentionBackgroundService(
    IServiceScopeFactory scopeFactory,
    IdentityRetentionOptions options,
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
                    .GetRequiredService<IIdentityRetentionService>();
                var result = await retention
                    .CleanupAsync(options, stoppingToken)
                    .ConfigureAwait(false);
                Console.WriteLine(
                    $"identity retention cleanup at {clock.GetUtcNow():O}: " +
                    $"sessions={result.DeletedSessions}, access_tokens={result.DeletedAccessTokens}.");
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                Console.WriteLine(
                    $"identity retention cleanup failed: {exception.GetType().Name}.");
            }

            await Task.Delay(Interval, stoppingToken).ConfigureAwait(false);
        }
    }
}
