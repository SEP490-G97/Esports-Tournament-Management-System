using System.Threading.Channels;
using ETOMS_API.Services.Interface;

namespace ETOMS_API.Services.Impl;

/// <summary>Một email chờ gửi (thông báo trong hệ thống gửi thêm qua email cho người bật "Nhận thông báo qua email").</summary>
public sealed record QueuedMail(string To, string Subject, string HtmlBody);

/// <summary>
/// Hàng đợi email trong bộ nhớ: lưu thông báo xong thì xếp email vào đây và trả lời người dùng ngay, việc gửi qua SMTP
/// (vài giây) chạy nền ở <see cref="NotificationMailWorker"/>. Dùng cho Repositories/NotificationEmailInterceptor.
/// </summary>
public sealed class NotificationMailQueue
{
    private readonly Channel<QueuedMail> _channel =
        Channel.CreateUnbounded<QueuedMail>(new UnboundedChannelOptions { SingleReader = true });

    public bool TryEnqueue(QueuedMail mail) => _channel.Writer.TryWrite(mail);

    public ChannelReader<QueuedMail> Reader => _channel.Reader;
}

/// <summary>Gửi lần lượt các email trong hàng đợi bằng IEmailSender; lỗi một email chỉ ghi log, không dừng hàng đợi.</summary>
public sealed class NotificationMailWorker : BackgroundService
{
    private readonly NotificationMailQueue _queue;
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<NotificationMailWorker> _log;

    public NotificationMailWorker(NotificationMailQueue queue, IServiceScopeFactory scopes, ILogger<NotificationMailWorker> log)
    {
        _queue = queue;
        _scopes = scopes;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var mail in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var scope = _scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<IEmailSender>().SendAsync(mail.To, mail.Subject, mail.HtmlBody, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _log.LogWarning(ex, "Gửi email thông báo tới {To} thất bại", mail.To);
            }
        }
    }
}
