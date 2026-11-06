using ETOMS_API.DTOs.Home;
using ETOMS_API.Models;
using ETOMS_API.Repositories;
using ETOMS_API.Services;
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

    // Đếm Teams/Players/Tournaments trong 1 round-trip thay vì 3 — database ở xa (MySQL
    // 15.135.76.150) nên gộp bớt lượt gọi qua lại giúp trang chủ bớt lag.
    private sealed record HomeCountsRow(int Teams, int Players, int Tournaments);

    // GET /api/home/summary — cache RAM 30 giây: dữ liệu trang chủ đổi chậm, database ở xa
    // nên lượt tải lại trong 30 giây không cần hỏi DB nữa (vẫn thấy dữ liệu mới sau đó).
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
            .Select(g => new GameCardDto { Title = g.Title, ShortCode = g.ShortCode, LogoUrl = g.LogoUrl })
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
        var featured = await _uow.Repository<Tournament>().Query()
            .Where(t => statuses.Contains(t.Status))
            .OrderBy(t => t.StartAt)
            .Select(t => new
            {
                t.TournamentId, t.Name, Game = t.Game.Title, t.Status, t.MaxTeams, t.BannerUrl, t.PrizeStructure, t.RegCloseAt,
                Teams = t.Registrations.Count(r => r.Status != "DRAFT" && r.Status != "REJECTED" && r.Status != "WITHDRAWN")
            })
            .Take(6).ToListAsync();

        var featuredList = featured.Select(t => new FeaturedTournamentDto
        {
            TournamentId = t.TournamentId,
            Name = t.Name, Game = t.Game,
            Prize = string.IsNullOrWhiteSpace(t.PrizeStructure) ? "Chưa công bố" : t.PrizeStructure,
            Teams = t.Teams, MaxTeams = t.MaxTeams,
            Status = StatusLabel(t.Status), StatusClass = StatusClass(t.Status),
            ImageUrl = string.IsNullOrWhiteSpace(t.BannerUrl) ? "/images/tournament-cover.svg" : t.BannerUrl!
        }).ToList();

        var events = featured.Select(t => new EventTickerDto
        {
            Label = t.Status == "ONGOING" ? "ĐANG DIỄN RA" : "ĐĂNG KÝ",
            Text = t.Status == "ONGOING" ? $"{t.Name} đang diễn ra"
                : $"Mở đăng ký {t.Name} — hạn chót {t.RegCloseAt:dd/MM/yyyy}"
        }).ToList();

        // Đội tuyển nổi bật — lấy từ module TEAM (Iteration 1), dữ liệu thật.
        var teams = (await _teamService.GetAllTeamsAsync())
            .Where(t => t.Status == "ACTIVE").Take(6).ToList();

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

        return new HomeSummaryDto
        {
            Stats = stats, Games = games, Featured = featuredList, Events = events, Teams = teams, News = news
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
        "REGISTRATION_OPEN" => "Đang mở đăng ký",
        "REGISTRATION_CLOSED" => "Đã đóng đăng ký",
        "ONGOING" => "Đang diễn ra",
        "PUBLISHED" => "Sắp diễn ra",
        _ => s
    };

    private static string StatusClass(string s) => s switch
    {
        "REGISTRATION_OPEN" or "ONGOING" => "badge-active",
        "PUBLISHED" => "badge-gold",
        _ => "badge-soft"
    };
}
