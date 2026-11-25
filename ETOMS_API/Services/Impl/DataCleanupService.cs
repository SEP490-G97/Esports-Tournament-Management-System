using ETOMS_API.Repositories;
using Microsoft.EntityFrameworkCore;

namespace ETOMS_API.Services.Impl;

// NUI-ADM-01 (Report 3 §3.1.4, UC-43): xóa mã mời đội đã hết hạn và token xác minh / đặt lại mật khẩu / làm mới đã hết hạn.
// Giữ lại 7 ngày sau khi hết hạn để người bấm liên kết cũ vẫn được báo "đã hết hạn" thay vì "không hợp lệ".
// Phần "hết hạn đơn chờ của giải đã đóng đăng ký" do WaitlistExpirationWorker làm mỗi phút (TournamentRegistrationService.ProcessWaitlistsAsync).
public sealed class DataCleanupService
{
    public static readonly TimeSpan Retention = TimeSpan.FromDays(7);
    private readonly EtomsDbContext _db;

    public DataCleanupService(EtomsDbContext db) => _db = db;

    public async Task<(int Invites, int Tokens)> RunAsync(DateTime nowUtc, CancellationToken ct = default)
    {
        var cutoff = nowUtc - Retention;
        var invites = await _db.Teams
            .Where(t => t.InviteCode != null && t.InviteExpiresAt != null && t.InviteExpiresAt <= cutoff)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.InviteCode, (string?)null).SetProperty(t => t.InviteExpiresAt, (DateTime?)null), ct);
        var tokens = await _db.AuthTokens.Where(a => a.ExpiresAt <= cutoff).ExecuteDeleteAsync(ct);
        return (invites, tokens);
    }
}

// Chạy NUI-ADM-01 mỗi giờ.
public sealed class DataCleanupWorker : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DataCleanupWorker> _logger;

    public DataCleanupWorker(IServiceScopeFactory scopeFactory, ILogger<DataCleanupWorker> logger)
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
                var (invites, tokens) = await scope.ServiceProvider.GetRequiredService<DataCleanupService>().RunAsync(DateTime.UtcNow, stoppingToken);
                if (invites + tokens > 0) _logger.LogInformation("Cleanup removed {Invites} invite code(s) and {Tokens} token(s).", invites, tokens);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Data cleanup job failed.");
            }
        }
    }
}
