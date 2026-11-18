using System.Threading.Channels;
using ETOMS_API.Services;
using ETOMS_API.Services.Interface;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ETOMS_API.Services.Impl;

public class MailQueue : IMailQueue
{
    private readonly Channel<QueuedMail> _channel =
        Channel.CreateUnbounded<QueuedMail>(new UnboundedChannelOptions { SingleReader = true });

    public bool TryEnqueue(QueuedMail mail) => _channel.Writer.TryWrite(mail);

    public ChannelReader<QueuedMail> Reader => _channel.Reader;
}

/// <summary>Đọc hàng đợi và gửi lần lượt qua IEmailSender (MailKit hoặc LogEmailSender tùy cấu hình).</summary>
public class MailQueueWorker : BackgroundService
{
    private readonly IMailQueue _queue;
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<MailQueueWorker> _log;

    public MailQueueWorker(IMailQueue queue, IServiceScopeFactory scopes, ILogger<MailQueueWorker> log)
    {
        _queue = queue;
        _scopes = scopes;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var mail in _queue.Reader.ReadAllAsync(stoppingToken))
            {
                try
                {
                    using var scope = _scopes.CreateScope();
                    var sender = scope.ServiceProvider.GetRequiredService<IEmailSender>();
                    await sender.SendAsync(mail.To, mail.Subject, mail.HtmlBody, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    // Giống các luồng mail khác: lỗi gửi chỉ ghi log, không ảnh hưởng nghiệp vụ.
                    _log.LogWarning(ex, "Gửi mail hàng loạt tới {To} thất bại", mail.To);
                }

                // Giãn nhẹ để không dồn dập vào SMTP (Gmail cá nhân giới hạn ~500 mail/ngày).
                try { await Task.Delay(100, stoppingToken); }
                catch (OperationCanceledException) { break; }
            }
        }
        catch (OperationCanceledException)
        {
            // dừng ứng dụng
        }
    }
}