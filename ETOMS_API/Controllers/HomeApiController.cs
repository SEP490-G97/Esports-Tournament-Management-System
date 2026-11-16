using ETOMS_API.DTOs.Home;
using ETOMS_API.Models;
using ETOMS_API.Repositories;
using ETOMS_API.Repositories.Interface;
using ETOMS_API.Services.Interface;
using ETOMS_API.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SubscribeRequest = ETOMS_API.DTOs.Home.SubscribeRequest;

namespace ETOMS_API.Controllers;

// API JSON cho trang chủ — gộp Thống kê/Game/Giải đấu nổi bật/Ticker/Đội nổi bật trong 1 lần gọi,
// để trang tĩnh index.html chỉ cần 1 fetch khi tải trang. Logic y hệt HomeController (MVC) cũ.
[Route("api/home")]
public class HomeApiController : ApiControllerBase
{
    private const string SummaryCacheKey = "home-summary";

    private readonly IUnitOfWork _uow;
    private readonly ITeamService _teamService;
    private readonly EtomsDbContext _db;
    private readonly IMemoryCache _cache;

    public HomeApiController(IUnitOfWork uow, ITeamService teamService, EtomsDbContext db, IMemoryCache cache)
    {
        _uow = uow;
        _teamService = teamService;
        _db = db;
        _cache = cache;
    }

    // Đếm Teams/Players/Tournaments trong 1 round-trip thay vì 3 — database ở xa nên gộp bớt lượt gọi qua lại.
    private sealed record HomeCountsRow(int Teams, int Players, int Tournaments);

    // GET /api/home/summary — cache RAM 30 giây: dữ liệu trang chủ đổi chậm, database ở xa
    // nên lượt tải lại trong 30 giây không cần hỏi DB nữa.
    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary()
    {
        var cached = await _cache.GetOrCreateAsync(SummaryCacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30);
            return await BuildSummaryAsync();
        });
        return Ok(cached);
    }

    private async Task<HomeSummaryDto> BuildSummaryAsync()
    {
        var games = await _uow.Repository<Game>().Query()
            .Where(g => g.Status == "ACTIVE")
            .OrderBy(g => g.Title)
            .Select(g => new GameCardDto { GameId = g.GameId, Title = g.Title, ShortCode = g.ShortCode, LogoUrl = g.LogoUrl,
                // Số giải công khai của game (ô tựa game ở trang chủ bấm vào lọc giải theo game).
                TournamentCount = g.Tournaments.Count(t => t.Status != "DRAFT" && t.Status != "CANCELLED") })
            .ToListAsync();

        var counts = await _db.Database.SqlQuery<HomeCountsRow>($@"
            SELECT
                (SELECT COUNT(*) FROM Team WHERE Status = 'ACTIVE') AS Teams,
                (SELECT COUNT(*) FROM Account WHERE Status = 'ACTIVE') AS Players,
                (SELECT COUNT(*) FROM Tournament WHERE Status NOT IN ('DRAFT','CANCELLED')) AS Tournaments")
            .FirstAsync();

        var stats = new HomeStatsDto
        {
            Games = games.Count,
            Teams = counts.Teams,
            Players = counts.Players,
            Tournaments = counts.Tournaments
        };

        // Giải đấu nổi bật — từ bảng Tournament THẬT.
        var statuses = new[] { "PUBLISHED", "ONGOING" };
        var now = DateTime.UtcNow;
        var featured = await _uow.Repository<Tournament>().Query()
            .Where(t => statuses.Contains(t.Status))
            // Giải đang mở đăng ký lên trước — nếu không, 6 ô bị các giải đang diễn ra (khai mạc sớm hơn)
            // chiếm hết và người chơi không tìm thấy giải nào để đăng ký.
            .OrderByDescending(t => t.Status == "PUBLISHED" && t.BracketLockedAt == null && t.RegOpenAt <= now && now < t.RegCloseAt)
            .ThenBy(t => t.StartAt)
            .Select(t => new
            {
                t.TournamentId, t.Name, Game = t.Game.Title, t.Status, t.MaxTeams, t.BannerUrl, t.PrizeStructure,
                t.RegOpenAt, t.RegCloseAt, t.BracketLockedAt, t.StartAt,
                Teams = t.Registrations.Count(r => r.Status != "DRAFT" && r.Status != "REJECTED" && r.Status != "WITHDRAWN")
            })
            .Take(12).ToListAsync();   // trang chủ hiện 4 giải/lượt, bấm ‹ › để trượt

        var featuredList = featured.Select(t => new FeaturedTournamentDto
        {
            TournamentId = t.TournamentId,
            Name = t.Name, Game = t.Game,
            Prize = string.IsNullOrWhiteSpace(t.PrizeStructure) ? "Chưa công bố" : t.PrizeStructure,
            Teams = t.Teams, MaxTeams = t.MaxTeams,
            Status = StatusLabel(t.Status), StatusClass = StatusClass(t.Status),
            RegistrationOpen = t.Status == "PUBLISHED"
                && t.BracketLockedAt is null
                && t.RegOpenAt <= now && now < t.RegCloseAt,
            ImageUrl = string.IsNullOrWhiteSpace(t.BannerUrl) ? "/images/tournament-cover.svg" : t.BannerUrl!
        }).ToList();

        // Dòng chạy "SỰ KIỆN": nhãn theo trạng thái THẬT của giải (không phải giải đã xuất bản nào cũng đang
        // nhận đăng ký). Giải đang mở đăng ký -> bấm vào trang đăng ký của chính giải đó; còn lại -> trang giải.
        // Ngày hiển thị theo giờ Việt Nam (DB lưu UTC).
        static string VnDate(DateTime utc) => utc.AddHours(7).ToString("dd/MM/yyyy");
        var events = featured.Select(t =>
        {
            var open = t.Status == "PUBLISHED" && t.BracketLockedAt is null && t.RegOpenAt <= now && now < t.RegCloseAt;
            var overview = "/bracket/view.html?id=" + t.TournamentId;
            if (t.Status == "ONGOING")
                return new EventTickerDto { Label = "ĐANG DIỄN RA", Text = $"{t.Name} đang diễn ra", Url = "/tournament/live.html?id=" + t.TournamentId, Kind = "LIVE" };
            if (open)
                return new EventTickerDto { Label = "ĐĂNG KÝ", Text = $"Mở đăng ký {t.Name} — hạn chót {VnDate(t.RegCloseAt)}", Url = "/registration/register.html?tournamentId=" + t.TournamentId, Kind = "REGISTER" };
            if (now < t.RegOpenAt)
                return new EventTickerDto { Label = "SẮP MỞ ĐĂNG KÝ", Text = $"{t.Name} — mở đăng ký từ {VnDate(t.RegOpenAt)}", Url = overview, Kind = "UPCOMING" };
            return new EventTickerDto { Label = "SẮP KHỞI TRANH", Text = $"{t.Name} — đã đóng đăng ký, khai mạc {VnDate(t.StartAt)}", Url = overview, Kind = "UPCOMING" };
        }).ToList();

        // Đội tuyển nổi bật — chỉ đội đủ người thi đấu, lọc + sắp xếp + giới hạn ở SQL, lấy 50 rồi chọn 6 trong bộ nhớ.
        var candidates = await _uow.Repository<Team>().Query()
            .Where(t => t.Status == "ACTIVE"
                && t.TeamMembers.Count(m => m.Status == "ACTIVE" && m.Slot == "STARTER") >= t.Game.MinStarters)
            .OrderByDescending(t => t.MatchWinnerTeams.Count(m => m.Status == "VERIFIED"))
            .ThenByDescending(t => t.TeamMembers.Count(m => m.Status == "ACTIVE"))
            .ThenBy(t => t.Name)
            .Take(50)
            .Select(t => new
            {
                t.TeamId, t.Name, t.Tag, t.LogoUrl, t.GameId, GameTitle = t.Game.Title,
                CaptainName = t.CaptainAccount.FullName,
                Members = t.TeamMembers.Count(m => m.Status == "ACTIVE"),
                MaxRoster = t.Game.MaxStarters + t.Game.MaxSubstitutes,
                Wins = t.MatchWinnerTeams.Count(m => m.Status == "VERIFIED")
            })
            .ToListAsync();
        var picked = candidates.GroupBy(t => t.GameId).Select(g => g.First()).ToList();
        picked.AddRange(candidates.Where(t => !picked.Contains(t)));
        var teams = picked.Take(6).Select(t => new HomeTeamDto
        {
            TeamId = t.TeamId, Name = t.Name, Tag = t.Tag, LogoUrl = t.LogoUrl, GameTitle = t.GameTitle, CaptainName = t.CaptainName,
            MemberCount = t.Members, MaxRoster = t.MaxRoster, Wins = t.Wins
        }).ToList();

        // Tin tức — ContentItem Kind='NEWS', Status='PUBLISHED'. Tin ghim (IsPinned) lên đầu,
        // còn lại xếp theo ngày đăng mới nhất (UC-13, UC-43).
        var news = await _uow.Repository<ContentItem>().Query()
            .Where(c => c.Kind == "NEWS" && c.Status == "PUBLISHED")
            .OrderByDescending(c => c.IsPinned)
            .ThenByDescending(c => c.PublishedAt)
            .Take(5)
            .Select(c => new NewsItemDto
            {
                ContentItemId = c.ContentItemId,
                Title = c.Title,
                Excerpt = c.Body.Length > 140 ? c.Body.Substring(0, 140) + "…" : c.Body,
                IsPinned = c.IsPinned,
                PublishedAt = c.PublishedAt
            })
            .ToListAsync();

        // Nhà tài trợ (UC-42) của các giải đang công khai — hạng cao lên trước. Giải đã hủy chỉ hiện khi
        // Ban tổ chức chọn giữ logo (ShowWhenCancelled). Chỉ lấy 3 cho khu "Nhà tài trợ" trên trang chủ.
        var sponsorRows = await _uow.Repository<TournamentSponsor>().Query()
            .Where(s => s.Tournament.Status == "PUBLISHED" || s.Tournament.Status == "ONGOING" || s.Tournament.Status == "COMPLETED"
                     || (s.Tournament.Status == "CANCELLED" && s.ShowWhenCancelled))
            .OrderBy(s => s.Tier == "DIAMOND" ? 0 : s.Tier == "GOLD" ? 1 : s.Tier == "SILVER" ? 2 : s.Tier == "BRONZE" ? 3 : 4)
            .ThenBy(s => s.Tournament.Status == "ONGOING" ? 0 : s.Tournament.Status == "PUBLISHED" ? 1 : 2)
            .ThenBy(s => s.DisplayOrder)
            .Take(3)
            .Select(s => new { s.Name, s.LogoUrl, s.WebsiteUrl, s.Tier, s.TournamentId, TournamentName = s.Tournament.Name })
            .ToListAsync();
        var sponsors = sponsorRows.Select(s => new HomeSponsorDto
        {
            Name = s.Name, LogoUrl = s.LogoUrl, WebsiteUrl = s.WebsiteUrl, Tier = s.Tier,
            TierLabel = s.Tier switch
            {
                "DIAMOND" => "Kim cương", "GOLD" => "Vàng", "SILVER" => "Bạc", "BRONZE" => "Đồng", _ => "Đối tác"
            },
            TournamentId = s.TournamentId, TournamentName = s.TournamentName
        }).ToList();

        // Livestream ĐANG PHÁT (UC-46) của giải không phải Nháp/đã hủy — hiện khu "Đang phát trực tiếp".
        var liveRows = await _uow.Repository<Livestream>().Query()
            .Where(l => l.Status == "LIVE" && l.Tournament.Status != "DRAFT" && l.Tournament.Status != "CANCELLED")
            .OrderByDescending(l => l.CreatedAt)
            .Take(4)
            .Select(l => new
            {
                l.LivestreamId, l.TournamentId, TournamentName = l.Tournament.Name, l.Title, l.Platform,
                TeamA = l.Match != null && l.Match.TeamA != null ? l.Match.TeamA.Name : null,
                TeamB = l.Match != null && l.Match.TeamB != null ? l.Match.TeamB.Name : null
            })
            .ToListAsync();
        var liveNow = liveRows.Select(l => new HomeLiveDto
        {
            LivestreamId = l.LivestreamId, TournamentId = l.TournamentId, TournamentName = l.TournamentName,
            Title = !string.IsNullOrWhiteSpace(l.Title) ? l.Title!
                : l.TeamA != null ? $"{l.TeamA} vs {l.TeamB ?? "Chưa xác định"}" : "Phát trực tiếp " + l.TournamentName,
            PlatformLabel = ETOMS_API.Utils.StreamEmbed.PlatformLabel(l.Platform)
        }).ToList();

        return new HomeSummaryDto
        {
            Stats = stats, Games = games, Featured = featuredList, Events = events, Teams = teams, News = news,
            Sponsors = sponsors, LiveNow = liveNow
        };
    }

    // POST /api/home/subscribe
    [HttpPost("subscribe")]
    public IActionResult Subscribe([FromBody] SubscribeRequest subscribe)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        return Ok(new { message = $"Đã đăng ký nhận tin cho {subscribe.Email}. Cảm ơn bạn!" });
    }

    private static string StatusLabel(string s) => s switch
    {
        "ONGOING" => "Đang diễn ra",
        "PUBLISHED" => "Sắp diễn ra",
        _ => s
    };

    private static string StatusClass(string s) => s switch
    {
        "ONGOING" => "badge-active",
        "PUBLISHED" => "badge-gold",
        _ => "badge-soft"
    };
}
