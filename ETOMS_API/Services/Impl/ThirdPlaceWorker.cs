using ETOMS_API.Services.Interface;

namespace ETOMS_API.Services.Impl;

// BRK-09: định kỳ tạo trận tranh hạng Ba và đưa đội thua bán kết vào ngay khi bán kết có kết quả.
public sealed class ThirdPlaceWorker : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ThirdPlaceWorker> _logger;

    public ThirdPlaceWorker(IServiceScopeFactory scopeFactory, ILogger<ThirdPlaceWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var service = scope.ServiceProvider.GetRequiredService<IThirdPlaceService>();
                var changed = await service.RunScheduledAsync(stoppingToken);
                if (changed > 0) _logger.LogInformation("Third place match updated for {Count} tournament(s).", changed);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Third place match job failed.");
            }
        }
    }
}
