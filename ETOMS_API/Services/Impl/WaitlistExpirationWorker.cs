using ETOMS_API.Services.Interface;

namespace ETOMS_API.Services.Impl;

public sealed class WaitlistExpirationWorker : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<WaitlistExpirationWorker> _logger;

    public WaitlistExpirationWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<WaitlistExpirationWorker> logger)
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
                var registrationService = scope.ServiceProvider.GetRequiredService<ITournamentRegistrationService>();
                await registrationService.ProcessWaitlistsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Waitlist expiration and promotion job failed.");
            }
        }
    }
}
