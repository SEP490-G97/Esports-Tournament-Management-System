using ETOMS_API.Services.Interface;

namespace ETOMS_API.Services.Impl;

// RES-03 (UC-29) — tác nhân Scheduler: mỗi phút tìm trận đã quá hạn báo cáo mà chưa đủ hai bản,
// chuyển sang tranh chấp "Quá hạn báo cáo". Mỗi trận chạy trong scope riêng: một trận lỗi
// (ví dụ đụng chỉ mục một-tranh-chấp-đang-mở) không làm kẹt các trận còn lại.
public sealed class ReportTimeoutWorker : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ReportTimeoutWorker> _logger;

    public ReportTimeoutWorker(IServiceScopeFactory scopeFactory, ILogger<ReportTimeoutWorker> logger)
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
                await RunOnceAsync(DateTime.UtcNow, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Report timeout job failed.");
            }
        }
    }

    private async Task RunOnceAsync(DateTime nowUtc, CancellationToken ct)
    {
        IReadOnlyList<int> due;
        await using (var scope = _scopeFactory.CreateAsyncScope())
            due = await scope.ServiceProvider.GetRequiredService<IResultService>().GetOverdueReportMatchIdsAsync(nowUtc, ct);

        foreach (var matchId in due)
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var closed = await scope.ServiceProvider.GetRequiredService<IResultService>()
                    .CloseReportWindowAsync(matchId, nowUtc, ct);
                if (closed) _logger.LogInformation("Match {MatchId} moved to dispute: report deadline passed.", matchId);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogError(exception, "Closing report window failed for match {MatchId}.", matchId);
            }
        }
    }
}
