using ETOMS_API.Repositories;
using ETOMS_API.Services.Interface;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace ETOMS_API.Controllers;

// Thông báo trong web của người đang đăng nhập (bảng Notification) — chuông ở đầu trang (layout.js).
// Mọi chức năng đều đã ghi Notification (duyệt đơn, mời đội, kết quả trận...) nhưng trước đây không có chỗ xem.
[ApiController]
[Route("api/notifications")]
public sealed class NotificationsController : ControllerBase
{
    private readonly EtomsDbContext _db;
    private readonly ICurrentUser _me;

    public NotificationsController(EtomsDbContext db, ICurrentUser me)
    {
        _db = db;
        _me = me;
    }

    // Kênh EMAIL là thông báo chỉ gửi email (không hiện trong web).
    private IQueryable<Models.Notification> Mine(int id) =>
        _db.Notifications.Where(n => n.AccountId == id && n.Channel != "EMAIL");

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken ct = default)
    {
        if (_me.UserId is not int id) return Unauthorized();
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 30);
        var q = Mine(id).AsNoTracking();
        var total = await q.CountAsync(ct);
        var unread = await q.CountAsync(n => !n.IsRead, ct);
        var items = await q.OrderByDescending(n => n.SentAt).ThenByDescending(n => n.NotificationId)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(n => new { n.NotificationId, n.NotificationType, n.Title, n.Body, n.LinkUrl, n.IsRead, n.SentAt })
            .ToListAsync(ct);
        // Thông báo ghi trước khi có LinkUrl (hoặc do bản code cũ ghi) thì suy ra trang đích để bấm vào vẫn đi đúng chỗ.
        var guessed = new Dictionary<int, string?>();
        foreach (var n in items.Where(n => string.IsNullOrEmpty(n.LinkUrl)))
            guessed[n.NotificationId] = await GuessLinkAsync(id, n.Title, n.Body, ct);
        var result = items.Select(n => new
        {
            n.NotificationId, n.NotificationType, n.Title, n.Body,
            LinkUrl = string.IsNullOrEmpty(n.LinkUrl) ? guessed.GetValueOrDefault(n.NotificationId) : n.LinkUrl,
            n.IsRead, n.SentAt
        });
        Response.Headers.CacheControl = "no-store";
        return Ok(new { total, unread, page, pageSize, items = result });
    }

    private const string Tbd = "Chờ xác định";
    private static readonly Regex RefereeBody = new(@"[Tt]rận (?<a>.+?) vs (?<b>.+?) của giải (?<t>.+?)(?: do .+ làm trọng tài)?\.$", RegexOptions.Compiled);
    private static readonly Regex ChangedBody = new(@"^Ban tổ chức giải (?<t>.+?) vừa thay đổi", RegexOptions.Compiled);

    // Chỉ dựa vào dữ liệu người nhận được thấy: trận mình làm trọng tài hoặc đội mình làm đội trưởng.
    private async Task<string?> GuessLinkAsync(int me, string title, string? body, CancellationToken ct)
    {
        body ??= "";
        if (title == "Phân công trọng tài" && RefereeBody.Match(body) is { Success: true } m)
        {
            var tName = m.Groups["t"].Value;
            string a = m.Groups["a"].Value, b = m.Groups["b"].Value;
            var tourIds = await _db.Tournaments.AsNoTracking().Where(t => t.Name == tName).Select(t => t.TournamentId).ToListAsync(ct);
            if (tourIds.Count == 0) return body.StartsWith("Bạn được gán") ? "/referee/index.html" : null;
            var cands = await _db.Matches.AsNoTracking()
                .Where(x => tourIds.Contains(x.TournamentId)
                    && (x.RefereeAccountId == me || (x.TeamA != null && x.TeamA.CaptainAccountId == me) || (x.TeamB != null && x.TeamB.CaptainAccountId == me)))
                .OrderBy(x => x.MatchId)
                .Select(x => new { x.MatchId, x.TournamentId, A = x.TeamA != null ? x.TeamA.Name : null, B = x.TeamB != null ? x.TeamB.Name : null })
                .ToListAsync(ct);
            // Khớp đúng tên hai đội; "Chờ xác định" lúc gửi có thể đã thành đội thật nên khớp lỏng nếu không có cái đúng.
            bool Exact(string? name, string want) => want == Tbd ? name == null : name == want;
            bool Loose(string? name, string want) => want == Tbd || name == want;
            var exact = cands.Where(x => Exact(x.A, a) && Exact(x.B, b)).ToList();
            var loose = cands.Where(x => Loose(x.A, a) && Loose(x.B, b)).ToList();
            var pick = exact.Count == 1 ? exact[0] : loose.Count == 1 ? loose[0] : null;
            if (pick != null) return "/match/detail.html?id=" + pick.MatchId;
            return body.StartsWith("Bạn được gán") ? "/referee/index.html" : "/tournament/schedule.html?id=" + tourIds[0];
        }
        if (title == "Giải thay đổi thông tin" && ChangedBody.Match(body) is { Success: true } c)
        {
            var tName = c.Groups["t"].Value;
            var tid = await _db.Tournaments.AsNoTracking().Where(t => t.Name == tName).Select(t => (int?)t.TournamentId).FirstOrDefaultAsync(ct);
            return tid is int x ? "/bracket/view.html?id=" + x : null;
        }
        if (title == "Lời mời vào ban tổ chức") return "/organization/index.html?mine=1";
        return null;
    }

    [HttpGet("unread-count")]
    public async Task<IActionResult> UnreadCount(CancellationToken ct)
    {
        if (_me.UserId is not int id) return Unauthorized();
        Response.Headers.CacheControl = "no-store";
        return Ok(new { unread = await Mine(id).CountAsync(n => !n.IsRead, ct) });
    }

    [HttpPost("{notificationId:int}/read")]
    public async Task<IActionResult> MarkRead(int notificationId, CancellationToken ct)
    {
        if (_me.UserId is not int id) return Unauthorized();
        // Chỉ đúng một thông báo, và chỉ của chính người đang đăng nhập.
        var n = await Mine(id).Where(x => x.NotificationId == notificationId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsRead, true), ct);
        return n == 0 ? NotFound(new { message = "Không tìm thấy thông báo." }) : Ok(new { message = "Đã đánh dấu đã đọc." });
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken ct)
    {
        if (_me.UserId is not int id) return Unauthorized();
        var n = await Mine(id).Where(x => !x.IsRead).ExecuteUpdateAsync(s => s.SetProperty(x => x.IsRead, true), ct);
        return Ok(new { message = n > 0 ? $"Đã đánh dấu {n} thông báo là đã đọc." : "Không có thông báo chưa đọc." });
    }
}
