using ETOMS_API.Services.Interface;

namespace ETOMS_API.Services.Impl;

public sealed class CompletedTournamentRankingWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CompletedTournamentRankingWorker> _logger;

    public CompletedTournamentRankingWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<CompletedTournamentRankingWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(2));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var rankingService = scope.ServiceProvider.GetRequiredService<IRankingService>();
                await rankingService.FinalizeCompletedTournamentsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Không thể hoàn tất ghi nhận danh hiệu cho các giải đã kết thúc.");
            }

            if (!await timer.WaitForNextTickAsync(stoppingToken))
            {
                break;
            }
        }
    }
}
