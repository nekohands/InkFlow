using InkFlow.Modules.Content.Application;

namespace InkFlow.Worker;

/// <summary>
/// 书籍包后台消费：按数据库租约串行生成，文件保留由同一循环定期清理。
/// </summary>
internal sealed class BookPackagePollingService(
    IServiceScopeFactory scopeFactory,
    TimeProvider clock,
    BookPackageOptions options) : BackgroundService
{
    private readonly string _owner = $"package-worker-{Environment.MachineName}"[..Math.Min(
        128,
        $"package-worker-{Environment.MachineName}".Length)];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var packages = scope.ServiceProvider.GetRequiredService<IBookPackageJobRepository>();
                var service = scope.ServiceProvider.GetRequiredService<BookPackageService>();
                var now = clock.GetUtcNow();
                var job = await packages
                    .TryLeaseAsync(now, _owner, options.LeaseDuration, stoppingToken)
                    .ConfigureAwait(false);
                if (job is not null)
                {
                    await service.ProcessAsync(job, stoppingToken).ConfigureAwait(false);
                    continue;
                }

                await service.ExpireOldAsync(stoppingToken).ConfigureAwait(false);
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception)
            {
                Console.Error.WriteLine("book package worker iteration failed.");
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }
    }
}
