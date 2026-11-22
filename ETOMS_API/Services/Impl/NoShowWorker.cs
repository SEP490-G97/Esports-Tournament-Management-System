using ETOMS_API.Services.Interface;

namespace ETOMS_API.Services.Impl;

// NUI-MAT-02 — tác nhân Scheduler: mỗi phút xử thắng trắng các trận đã quá giờ thi đấu mà chỉ một đội điểm danh.
public sealed class NoShowWorker : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<NoShowWorker> _logger;

    public NoShowWorker(IServiceScopeFactory scopeFactory, ILogger<NoShowWorker> logger)
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
                var done = await scope.ServiceProvider.GetRequiredService<IMatchOpsService>().ProcessNoShowsAsync(DateTime.UtcNow, stoppingToken);
                if (done > 0) _logger.LogInformation("No-show walkover awarded for {Count} match(es).", done);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "No-show job failed.");
            }
        }
    }
}
