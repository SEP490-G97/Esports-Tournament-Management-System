using System.Security.Claims;
using System.Text.RegularExpressions;
using ETOMS_API.Models;
using ETOMS_API.Repositories;
using ETOMS_API.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace ETOMS_API.Controllers;

// ADM-03: quản trị viên quản lý tựa game và luật mặc định của từng game (/admin/games.html) — bảng Game có sẵn.
// Luật mặc định = số người chính thức tối thiểu/tối đa, số dự bị, thể thức mặc định, loại tệp minh chứng và dung lượng.
// Bộ luật chi tiết (bản đồ, vị trí) quản lý ở /admin/game-rules.html (GRL-01..03).
// Không xóa game (giải, đội, bộ luật đều trỏ tới game) — chỉ ngừng (INACTIVE): game ngừng không cho tạo đội/giải mới.
[ApiController]
[Route("api/admin/games")]
[Authorize(Roles = AccountRoles.Admin)]
public sealed partial class AdminGamesController : ControllerBase
{
    private static readonly string[] Formats = { "SINGLE_ELIMINATION", "DOUBLE_ELIMINATION", "ROUND_ROBIN", "HYBRID" };

    [GeneratedRegex("^[A-Z0-9]{2,10}$")] private static partial Regex CodePattern();
    [GeneratedRegex("^[a-z0-9]{1,10}$")] private static partial Regex ExtPattern();

    private readonly EtomsDbContext _db;
    private readonly IWebHostEnvironment _env;
    private readonly IMemoryCache _cache;

    public AdminGamesController(EtomsDbContext db, IWebHostEnvironment env, IMemoryCache cache)
    {
        _db = db;
        _env = env;
        _cache = cache;
    }

    private ObjectResult Err(string message, int status = 400, string? field = null)
        => StatusCode(status, new { message, fieldErrors = field is null ? null : new Dictionary<string, string> { [field] = message } });

    // Danh sách game dùng chung được nhớ ngắn hạn ở vài nơi — đổi game thì xóa để trang khác thấy ngay.
    private void ClearLookups()
    {
        _cache.Remove("lookup:active-games");
        _cache.Remove("lookup:directory-filters");
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var rows = await _db.Games.AsNoTracking().OrderBy(g => g.Title).Select(g => new
        {
            g.GameId, g.Title, g.ShortCode, g.LogoUrl, g.Status, g.MinStarters, g.MaxStarters, g.MaxSubstitutes,
            g.DefaultFormat, g.AllowedEvidenceTypes, g.MaxEvidenceFileSizeMb,
            Teams = g.Teams.Count(t => t.Status == "ACTIVE"),
            Tournaments = g.Tournaments.Count(t => t.Status != "CANCELLED")
        }).ToListAsync(ct);
        var rulesets = await _db.GameRulesets.AsNoTracking().GroupBy(r => r.GameId)
            .Select(g => new { GameId = g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.GameId, x => x.Count, ct);
        Response.Headers.CacheControl = "no-store";
        return Ok(rows.Select(g => new
        {
            g.GameId, g.Title, g.ShortCode, g.LogoUrl, g.Status, g.MinStarters, g.MaxStarters, g.MaxSubstitutes,
            g.DefaultFormat, g.AllowedEvidenceTypes, g.MaxEvidenceFileSizeMb, g.Teams, g.Tournaments,
            Rulesets = rulesets.GetValueOrDefault(g.GameId)
        }));
    }

    public sealed class GameForm
    {
        public string? Title { get; set; }
        public string? ShortCode { get; set; }
        public int MinStarters { get; set; }
        public int MaxStarters { get; set; }
        public int MaxSubstitutes { get; set; }
        public string? DefaultFormat { get; set; }
        public string? AllowedEvidenceTypes { get; set; }
        public int MaxEvidenceFileSizeMb { get; set; } = 10;
        public IFormFile? LogoFile { get; set; }
        public string? LogoLink { get; set; }
    }

    [HttpPost]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public Task<IActionResult> Create([FromForm] GameForm form, CancellationToken ct) => SaveAsync(null, form, ct);

    [HttpPut("{id:int}")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public Task<IActionResult> Update(int id, [FromForm] GameForm form, CancellationToken ct) => SaveAsync(id, form, ct);

    private async Task<IActionResult> SaveAsync(int? id, GameForm f, CancellationToken ct)
    {
        var title = (f.Title ?? "").Trim();
        var code = (f.ShortCode ?? "").Trim().ToUpperInvariant();
        var format = (f.DefaultFormat ?? "").Trim().ToUpperInvariant();
        if (title.Length < 2 || title.Length > 150) return Err("Tên game từ 2 đến 150 ký tự.", field: "Title");
        if (!CodePattern().IsMatch(code)) return Err("Mã viết tắt 2–10 ký tự, chỉ chữ in hoa không dấu và số (ví dụ LOL, FCO).", field: "ShortCode");
        if (f.MinStarters < 1 || f.MinStarters > 20) return Err("Số người chính thức tối thiểu từ 1 đến 20.", field: "MinStarters");
        if (f.MaxStarters < f.MinStarters || f.MaxStarters > 20) return Err("Số người chính thức tối đa phải từ số tối thiểu đến 20.", field: "MaxStarters");
        if (f.MaxSubstitutes < 0 || f.MaxSubstitutes > 10) return Err("Số dự bị từ 0 đến 10.", field: "MaxSubstitutes");
        if (!Formats.Contains(format)) return Err("Thể thức mặc định không hợp lệ.", field: "DefaultFormat");
        var exts = (f.AllowedEvidenceTypes ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => x.TrimStart('.').ToLowerInvariant()).Distinct().ToList();
        var extText = string.Join(",", exts);
        if (exts.Count == 0 || exts.Any(x => !ExtPattern().IsMatch(x)) || extText.Length > 200)
            return Err("Loại tệp minh chứng là đuôi tệp cách nhau bằng dấu phẩy, ví dụ: png,jpg,mp4.", field: "AllowedEvidenceTypes");
        if (f.MaxEvidenceFileSizeMb < 1 || f.MaxEvidenceFileSizeMb > 50) return Err("Dung lượng tệp minh chứng từ 1 đến 50 MB.", field: "MaxEvidenceFileSizeMb");

        Game? g = null;
        if (id is int gid)
        {
            g = await _db.Games.FirstOrDefaultAsync(x => x.GameId == gid, ct);
            if (g is null) return Err("Không tìm thấy game.", 404);
        }
        var lower = title.ToLower();
        if (await _db.Games.AnyAsync(x => x.Title.ToLower() == lower && x.GameId != (id ?? 0), ct)) return Err("Đã có game cùng tên.", field: "Title");
        if (await _db.Games.AnyAsync(x => x.ShortCode == code && x.GameId != (id ?? 0), ct)) return Err("Mã viết tắt đã được dùng.", field: "ShortCode");

        string? logo = g?.LogoUrl;
        if (f.LogoFile is not null || !string.IsNullOrWhiteSpace(f.LogoLink))
        {
            var (url, error) = await BannerImage.SaveAsync(f.LogoFile, f.LogoLink, _env, ct);
            if (error is not null) return Err(error.Replace("Ảnh bìa", "Logo"), field: "Logo");
            logo = url;
        }

        if (g is null)
        {
            g = new Game { Status = "ACTIVE" };
            _db.Games.Add(g);
        }
        g.Title = title;
        g.ShortCode = code;
        g.LogoUrl = logo;
        g.MinStarters = f.MinStarters;
        g.MaxStarters = f.MaxStarters;
        g.MaxSubstitutes = f.MaxSubstitutes;
        g.DefaultFormat = format;
        g.AllowedEvidenceTypes = extText;
        g.MaxEvidenceFileSizeMb = f.MaxEvidenceFileSizeMb;
        await _db.SaveChangesAsync(ct);
        ClearLookups();
        return Ok(new { message = id is null ? "Đã thêm game." : "Đã lưu game. Giải đã tạo giữ nguyên luật lúc tạo; luật mới áp dụng cho đội và giải tạo sau.", g.GameId });
    }

    // Ngừng / mở lại game. Ngừng: không tạo đội, giải mới cho game này; đội và giải đang có không đổi.
    [HttpPost("{id:int}/toggle")]
    public async Task<IActionResult> Toggle(int id, CancellationToken ct)
    {
        var g = await _db.Games.FirstOrDefaultAsync(x => x.GameId == id, ct);
        if (g is null) return Err("Không tìm thấy game.", 404);
        g.Status = g.Status == "ACTIVE" ? "INACTIVE" : "ACTIVE";
        await _db.SaveChangesAsync(ct);
        ClearLookups();
        return Ok(new { message = g.Status == "ACTIVE" ? $"Đã mở lại game {g.Title}." : $"Đã ngừng game {g.Title}. Đội và giải đang có không bị ảnh hưởng." });
    }
}
