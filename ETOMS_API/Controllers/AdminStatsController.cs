using ETOMS_API.Repositories;
using ETOMS_API.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace ETOMS_API.Controllers;

// Thống kê hệ thống cho quản trị viên (/admin/stats.html). Chỉ ĐỌC, đếm trực tiếp từ các bảng có sẵn.
[ApiController]
[Route("api/admin/stats")]
[Authorize(Roles = AccountRoles.Admin)]
public sealed class AdminStatsController : ControllerBase
{
    private readonly EtomsDbContext _db;
    private readonly IMemoryCache _cache;
    public AdminStatsController(EtomsDbContext db, IMemoryCache cache) { _db = db; _cache = cache; }

    private static readonly Dictionary<string, string> TournamentStatusLabel = new()
    {
        ["DRAFT"] = "Bản nháp", ["PUBLISHED"] = "Đã xuất bản", ["ONGOING"] = "Đang diễn ra",
        ["COMPLETED"] = "Đã kết thúc", ["CANCELLED"] = "Đã hủy"
    };

    // 9 truy vấn đếm/gom nhóm (database ở xa ~200ms/lượt) → giữ kết quả 60 giây: trang tổng quan và trang thống kê
    // của Admin mở lại / chuyển qua lại không phải đếm lại từ đầu. Số liệu thống kê chậm 1 phút là chấp nhận được.
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] int months = 6, CancellationToken ct = default)
    {
        months = Math.Clamp(months, 3, 12);
        Response.Headers.CacheControl = "no-store";
        var data = await _cache.GetOrCreateAsync("admin-stats-" + months, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60);
            // Không gắn token của yêu cầu: người xem rời trang giữa chừng thì vẫn đếm xong và giữ vào bộ nhớ đệm cho lần
            // mở sau (trước đây ném TaskCanceledException ở PerMonth mỗi lần chuyển trang khi đang tải).
            return await BuildAsync(months, CancellationToken.None);
        });
        return Ok(data);
    }

    private async Task<object> BuildAsync(int months, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var from = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-(months - 1));

        var accounts = await _db.Accounts.AsNoTracking().GroupBy(a => new { a.Role, a.Status })
            .Select(g => new { g.Key.Role, g.Key.Status, Count = g.Count() }).ToListAsync(ct);
        var tournaments = await _db.Tournaments.AsNoTracking().GroupBy(t => t.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() }).ToListAsync(ct);
        var matches = await _db.Matches.AsNoTracking().GroupBy(m => m.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() }).ToListAsync(ct);
        var teams = await _db.Teams.AsNoTracking().CountAsync(t => t.Status == "ACTIVE", ct);
        var registrations = await _db.Registrations.AsNoTracking().CountAsync(r => r.Status == "APPROVED", ct);

        // Theo tháng (giờ Việt Nam = UTC+7 để tháng khớp với người xem).
        async Task<List<(string Key, int Count)>> PerMonth(IQueryable<DateTime> dates)
        {
            var list = await dates.Where(d => d >= from).ToListAsync(ct);
            return Enumerable.Range(0, months).Select(i => from.AddMonths(i)).Select(m =>
                ($"{m.Month:00}/{m.Year}", list.Count(d => d.AddHours(7).Year == m.Year && d.AddHours(7).Month == m.Month))).ToList();
        }
        var newAccounts = await PerMonth(_db.Accounts.AsNoTracking().Select(a => a.CreatedAt));
        var newTournaments = await PerMonth(_db.Tournaments.AsNoTracking().Select(t => t.CreatedAt));
        var newRegistrations = await PerMonth(_db.Registrations.AsNoTracking().Where(r => r.SubmittedAt != null).Select(r => r.SubmittedAt!.Value));

        var topGames = await _db.Tournaments.AsNoTracking().Where(t => t.Status != "DRAFT")
            .GroupBy(t => t.Game.Title).Select(g => new { Label = g.Key, Count = g.Count() })
            .OrderByDescending(g => g.Count).Take(6).ToListAsync(ct);

        int Acc(string role) => accounts.Where(a => a.Role == role).Sum(a => a.Count);
        return new
        {
            tiles = new
            {
                players = Acc("PLAYER"),
                organizers = Acc("ORGANIZER"),
                banned = accounts.Where(a => a.Status == "BANNED").Sum(a => a.Count),
                teams,
                tournaments = tournaments.Sum(t => t.Count),
                ongoing = tournaments.Where(t => t.Status == "ONGOING").Sum(t => t.Count),
                approvedRegistrations = registrations,
                matchesCompleted = matches.Where(m => m.Status == "VERIFIED" || m.Status == "WALKOVER").Sum(m => m.Count),   // trận xong = VERIFIED / WALKOVER
                matchesTotal = matches.Sum(m => m.Count)
            },
            tournamentsByStatus = TournamentStatusLabel.Select(kv => new { label = kv.Value, count = tournaments.Where(t => t.Status == kv.Key).Sum(t => t.Count) }),
            topGames = topGames.Select(g => new { label = g.Label, count = g.Count }),
            newAccounts = newAccounts.Select(m => new { label = m.Key, count = m.Count }),
            newTournaments = newTournaments.Select(m => new { label = m.Key, count = m.Count }),
            newRegistrations = newRegistrations.Select(m => new { label = m.Key, count = m.Count }).ToList()
        };
    }
}
