using ETOMS_API.Services.Interface;
using ETOMS_API.Utils;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace ETOMS_API.Services.Impl
{
    public class MailKitEmailSender : IEmailSender
    {
        private readonly EmailOptions _opt;
        public MailKitEmailSender(IOptions<EmailOptions> opt) => _opt = opt.Value;

        public async Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default)
        {
            var msg = new MimeMessage();
            msg.From.Add(new MailboxAddress(_opt.FromName, _opt.FromAddress));
            msg.To.Add(MailboxAddress.Parse(toEmail));
            msg.Subject = subject;
            msg.Body = new BodyBuilder { HtmlBody = htmlBody }.ToMessageBody();

            using var client = new SmtpClient();
            await client.ConnectAsync(_opt.Host, _opt.Port,
                _opt.UseStartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto, ct);
            if (!string.IsNullOrEmpty(_opt.User))
                await client.AuthenticateAsync(_opt.User, _opt.Password, ct);
            await client.SendAsync(msg, ct);
            await client.DisconnectAsync(true, ct);
        }
    }

    /// <summary>Dùng khi Email:Enabled = false: ghi nội dung mail (có link) ra log để copy test.</summary>
    public class LogEmailSender : IEmailSender
    {
        private readonly ILogger<LogEmailSender> _log;
        public LogEmailSender(ILogger<LogEmailSender> log) => _log = log;

        public Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default)
        {
            _log.LogWarning("[DEV MAIL] To={To} Subject={Subject}\n{Body}", toEmail, subject, htmlBody);
            return Task.CompletedTask;
        }
    }
}
