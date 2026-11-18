using ETOMS_API.Services.Interface;

namespace ETOMS_API.Services.Impl;

// BRK-08 (UC-18): Scheduler định kỳ chuyển các giải HYBRID đã xong vòng bảng sang knockout.
// Giải có đội hòa cần quyết định thì bỏ qua, chờ Ban tổ chức xếp ở trang chuyển giai đoạn.
public sealed class StageTransitionWorker : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<StageTransitionWorker> _logger;

    public StageTransitionWorker(IServiceScopeFactory scopeFactory, ILogger<StageTransitionWorker> logger)
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
                var service = scope.ServiceProvider.GetRequiredService<IStageTransitionService>();
                var advanced = await service.RunScheduledAsync(stoppingToken);
                if (advanced > 0) _logger.LogInformation("Stage transition created knockout brackets for {Count} tournament(s).", advanced);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Group to knockout stage transition job failed.");
            }
        }
    }
}
