using ETOMS_API.Services.Interface;

namespace ETOMS_API.Services.Impl;

// Mục 28: mỗi phút tự bắt đầu giải đã khóa nhánh khi tới giờ khai mạc, tự kết thúc giải đã xong mọi trận (ghi hạng chung cuộc).
public sealed class TournamentLifecycleWorker : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TournamentLifecycleWorker> _logger;

    public TournamentLifecycleWorker(IServiceScopeFactory scopeFactory, ILogger<TournamentLifecycleWorker> logger)
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
                var service = scope.ServiceProvider.GetRequiredService<ITournamentLifecycleService>();
                var changed = await service.RunScheduledAsync(stoppingToken);
                if (changed > 0) _logger.LogInformation("Tournament lifecycle changed {Count} tournament(s).", changed);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Tournament lifecycle job failed.");
            }
        }
    }
}
