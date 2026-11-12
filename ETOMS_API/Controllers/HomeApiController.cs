using ETOMS_API.DTOs.Home;
using ETOMS_API.Models;
using ETOMS_API.Repositories;
using ETOMS_API.Services.Interface;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SubscribeRequest = ETOMS_API.DTOs.Home.SubscribeRequest;

namespace ETOMS_API.Controllers;

// API JSON cho trang chủ — gộp Thống kê/Game/Giải đấu nổi bật/Ticker/Đội nổi bật trong 1 lần gọi,
// để trang tĩnh index.html chỉ cần 1 fetch khi tải trang. Logic y hệt HomeController (MVC) cũ.
[Route("api/home")]
public class HomeApiController : ApiControllerBase
{
    private readonly IUnitOfWork _uow;
    private readonly ITeamService _teamService;

    public HomeApiController(IUnitOfWork uow, ITeamService teamService)
    {
        _uow = uow;
        _teamService = teamService;
    }

    // GET /api/home/summary
    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary()
    {
        var games = await _uow.Repository<Game>().Query()
            .Where(g => g.Status == "ACTIVE")
            .OrderBy(g => g.Title)
            .Select(g => new GameCardDto { Title = g.Title, ShortCode = g.ShortCode, LogoUrl = g.LogoUrl })
            .ToListAsync();

        var stats = new HomeStatsDto
        {
            Games = games.Count,
            Teams = await _uow.Repository<Team>().Query().CountAsync(t => t.Status == "ACTIVE"),
            Players = await _uow.Repository<Account>().Query().CountAsync(a => a.Status == "ACTIVE"),
            Tournaments = await _uow.Repository<Tournament>().Query()
                .CountAsync(t => t.Status != "DRAFT" && t.Status != "CANCELLED")
        };

        // Giải đấu nổi bật — từ bảng Tournament THẬT.
        var statuses = new[] { "PUBLISHED", "ONGOING" };
        var now = DateTime.UtcNow;
        var featured = await _uow.Repository<Tournament>().Query()
            .Where(t => statuses.Contains(t.Status))
            .OrderBy(t => t.StartAt)
            .Select(t => new
            {
                t.TournamentId, t.Name, Game = t.Game.Title, t.Status, t.MaxTeams, t.BannerUrl, t.PrizeStructure,
                t.RegOpenAt, t.RegCloseAt, t.BracketLockedAt,
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
            RegistrationOpen = t.Status == "PUBLISHED"
                && t.BracketLockedAt is null
                && t.RegOpenAt <= now && now < t.RegCloseAt,
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

        return Ok(new HomeSummaryDto
        {
            Stats = stats, Games = games, Featured = featuredList, Events = events, Teams = teams
        });
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
