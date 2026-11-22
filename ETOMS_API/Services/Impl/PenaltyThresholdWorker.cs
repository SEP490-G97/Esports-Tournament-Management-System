using ETOMS_API.Services.Interface;

namespace ETOMS_API.Services.Impl;

// UC-35 — tác nhân Scheduler: mỗi phút xét tổng điểm phạt của các đội trong giải đang diễn ra; đội chạm ngưỡng
// bị tước quyền thi đấu và mọi trận còn lại được xử thắng trắng cho đối thủ. Cùng lượt quét xử nốt trận của đội
// đã bị loại khi đối thủ của trận đó vừa được xác định.
public sealed class PenaltyThresholdWorker : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PenaltyThresholdWorker> _logger;

    public PenaltyThresholdWorker(IServiceScopeFactory scopeFactory, ILogger<PenaltyThresholdWorker> logger)
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
                var changed = await scope.ServiceProvider.GetRequiredService<IDisciplineService>().RunScheduledAsync(stoppingToken);
                if (changed > 0) _logger.LogInformation("Penalty threshold job changed {Count} item(s).", changed);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Penalty threshold job failed.");
            }
        }
    }
}
