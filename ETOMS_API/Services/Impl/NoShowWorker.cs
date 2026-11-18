using ETOMS_API.Services.Interface;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ETOMS_API.Services.Impl;

/// <summary>
/// Chạy nền định kỳ gọi <see cref="INoShowService.ScanAsync"/>. Cấu hình: NoShow:Enabled (mặc định true),
/// NoShow:IntervalSeconds (mặc định 60, tối thiểu 10). Thiết kế cho MỘT instance; chạy nhiều instance cần khóa phân tán.
/// </summary>
public class NoShowWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<NoShowWorker> _log;
    private readonly TimeSpan _interval;
    private readonly bool _enabled;

    public NoShowWorker(IServiceScopeFactory scopes, ILogger<NoShowWorker> log, IConfiguration cfg)
    {
        _scopes = scopes;
        _log = log;
        _enabled = cfg.GetValue("NoShow:Enabled", true);
        _interval = TimeSpan.FromSeconds(Math.Max(10, cfg.GetValue("NoShow:IntervalSeconds", 60)));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_enabled) { _log.LogInformation("NoShowWorker đang tắt (NoShow:Enabled=false)"); return; }
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);   // chờ ứng dụng khởi động xong
            using var timer = new PeriodicTimer(_interval);
            do
            {
                try
                {
                    using var scope = _scopes.CreateScope();
                    var r = await scope.ServiceProvider.GetRequiredService<INoShowService>().ScanAsync(null, stoppingToken);
                    if (r.Walkovers + r.DoubleNoShows > 0 || r.ListenerFailures.Count > 0)
                        _log.LogInformation("No-show: quét {Scanned}, thắng trắng {W}, cả hai vắng {D}, lỗi bước sau {F}",
                            r.Scanned, r.Walkovers, r.DoubleNoShows, r.ListenerFailures.Count);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
                catch (Exception ex) { _log.LogError(ex, "Lượt quét No-show thất bại; sẽ thử lại ở lượt sau"); }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) { /* tắt ứng dụng */ }
    }
}