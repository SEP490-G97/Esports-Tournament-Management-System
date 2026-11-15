using ETOMS_API.Models;
using ETOMS_API.Repositories;
using ETOMS_API.Services.Interface;
using Microsoft.EntityFrameworkCore;

namespace ETOMS_API.Services.Impl;

// NOT-03 / UC-41 A1 — nhắc điểm danh trước giờ đấu 30 phút (BR-MATCH-01: cửa sổ điểm danh mở 30 phút trước
// giờ thi đấu, đóng đúng giờ thi đấu). Gửi cho đội trưởng của đội CHƯA điểm danh: luôn có thông báo trong app;
// email chỉ gửi khi người nhận bật nhận email (UC-41 A3). Gửi email lỗi thì thử lại một lần rồi bỏ qua (A2) —
// thông báo trong app vẫn là bản ghi, và hạn điểm danh không bao giờ lùi vì email (UC-41 Other Information).
public sealed class CheckInReminderService
{
    public const string NotificationType = "CHECKIN_REMINDER";
    private readonly EtomsDbContext _db;
    private readonly IEmailSender _email;
    private readonly ILogger<CheckInReminderService> _log;

    public CheckInReminderService(EtomsDbContext db, IEmailSender email, ILogger<CheckInReminderService> log)
    {
        _db = db;
        _email = email;
        _log = log;
    }

    public async Task<int> SendDueRemindersAsync(DateTime nowUtc, CancellationToken ct)
    {
        var due = await _db.Matches.AsNoTracking()
            .Where(m => m.Status == "PENDING" && m.ScheduledAt != null && m.CheckInOpenAt != null
                        && m.CheckInOpenAt <= nowUtc && m.ScheduledAt > nowUtc
                        && m.TeamAid != null && m.TeamBid != null
                        && m.Tournament.Status != "DRAFT" && m.Tournament.Status != "CANCELLED"
                        && (m.TeamAcheckedInAt == null || m.TeamBcheckedInAt == null))
            .Select(m => new
            {
                m.MatchId, m.ScheduledAt, TournamentName = m.Tournament.Name,
                TeamA = m.TeamA!.Name, TeamB = m.TeamB!.Name,
                CaptainA = m.TeamAcheckedInAt == null ? (int?)m.TeamA.CaptainAccountId : null,
                CaptainB = m.TeamBcheckedInAt == null ? (int?)m.TeamB.CaptainAccountId : null
            })
            .ToListAsync(ct);
        if (due.Count == 0) return 0;

        int sent = 0;
        foreach (var m in due)
        {
            var link = "/match/detail.html?id=" + m.MatchId;
            foreach (var captain in new[] { m.CaptainA, m.CaptainB }.Where(c => c.HasValue).Select(c => c!.Value).Distinct())
            {
                // Mỗi đội trưởng chỉ nhận MỘT lần nhắc cho mỗi trận (bản ghi Notification là dấu đã gửi).
                if (await _db.Notifications.AnyAsync(n => n.AccountId == captain && n.NotificationType == NotificationType && n.LinkUrl == link, ct))
                    continue;
                var account = await _db.Accounts.AsNoTracking()
                    .Where(a => a.AccountId == captain)
                    .Select(a => new { a.Email, a.FullName, a.NotifyEmail, a.Status })
                    .FirstOrDefaultAsync(ct);
                if (account is null || account.Status != "ACTIVE") continue;

                var kickOff = m.ScheduledAt!.Value.AddHours(7).ToString("HH:mm dd/MM/yyyy");
                var body = $"Trận {m.TeamA} vs {m.TeamB} ({m.TournamentName}) bắt đầu lúc {kickOff}. Cửa sổ điểm danh đang mở và đóng đúng giờ thi đấu — hãy vào trang trận bấm \"Điểm danh\".";
                _db.Notifications.Add(new Notification
                {
                    AccountId = captain,
                    NotificationType = NotificationType,
                    Title = "Nhắc điểm danh trước giờ thi đấu",
                    Body = body,
                    LinkUrl = link,
                    Channel = account.NotifyEmail ? "BOTH" : "IN_APP",
                    IsRead = false,
                    SentAt = nowUtc
                });
                await _db.SaveChangesAsync(ct);
                sent++;

                if (account.NotifyEmail) await SendEmailAsync(account.Email, account.FullName, body, ct);
            }
        }
        return sent;
    }

    private async Task SendEmailAsync(string to, string name, string body, CancellationToken ct)
    {
        var html = $"<p>Chào {System.Net.WebUtility.HtmlEncode(name)},</p><p>{System.Net.WebUtility.HtmlEncode(body)}</p><p>— ETOMS</p>";
        for (int attempt = 1; attempt <= 2; attempt++)
        {
            try { await _email.SendAsync(to, "[ETOMS] Nhắc điểm danh trước giờ thi đấu", html, ct); return; }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _log.LogWarning(ex, "Gửi email nhắc điểm danh lần {Attempt} thất bại.", attempt);
            }
        }
    }
}

public sealed class CheckInReminderWorker : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CheckInReminderWorker> _logger;

    public CheckInReminderWorker(IServiceScopeFactory scopeFactory, ILogger<CheckInReminderWorker> logger)
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
                var sent = await scope.ServiceProvider.GetRequiredService<CheckInReminderService>().SendDueRemindersAsync(DateTime.UtcNow, stoppingToken);
                if (sent > 0) _logger.LogInformation("Sent {Count} check-in reminder(s).", sent);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception) { _logger.LogError(exception, "Check-in reminder job failed."); }
        }
    }
}
