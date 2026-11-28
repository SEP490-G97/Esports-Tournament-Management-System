using ETOMS_API.Services.Impl;
using ETOMS_API.Utils;
using Microsoft.AspNetCore.Mvc;

namespace ETOMS_API.Controllers;

// Trang Tin tức (công khai): tin ETOMS + tin esports từ RSS báo (xem NewsFeedService).
[ApiController]
[Route("api/news")]
public sealed class NewsController : ControllerBase
{
    private readonly NewsFeedService _news;

    public NewsController(NewsFeedService news) => _news = news;

    private static readonly TimeSpan VnOffset = TimeSpan.FromHours(7);

    // ?source=all|etoms|gamek|tuoitre|thanhnien &game=all|LOL|VAL|… &from=yyyy-MM-dd &to=yyyy-MM-dd (ngày giờ Việt Nam)
    // &q=từ khóa &page=1 &pageSize=12
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? source, [FromQuery] string? q, [FromQuery] string? game,
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 12, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 30);
        var (items, sources) = await _news.GetAllAsync(ct);
        var games = await _news.GetGamesAsync(ct);

        var key = (source ?? "all").Trim().ToLowerInvariant();
        if (key != "all")
        {
            if (!sources.Any(s => s.Key == key)) return BadRequest(new { message = "Nguồn tin không hợp lệ." });
            items = items.Where(i => i.Source == key).ToList();
        }
        if (from is not null && to is not null && from > to)
            return BadRequest(new { message = "Ngày bắt đầu phải trước hoặc bằng ngày kết thúc." });
        if (from is DateOnly f)
        {
            var fromUtc = f.ToDateTime(TimeOnly.MinValue) - VnOffset;
            items = items.Where(i => i.PublishedAt >= fromUtc).ToList();
        }
        if (to is DateOnly t)
        {
            var toUtc = t.AddDays(1).ToDateTime(TimeOnly.MinValue) - VnOffset;
            items = items.Where(i => i.PublishedAt < toUtc).ToList();
        }
        var term = (q ?? string.Empty).Trim();
        if (term.Length > 100) return BadRequest(new { message = "Từ khóa tối đa 100 ký tự." });
        if (term.Length > 0)
            items = items.Where(i => i.Title.Contains(term, StringComparison.CurrentCultureIgnoreCase)
                || (i.Summary ?? string.Empty).Contains(term, StringComparison.CurrentCultureIgnoreCase)).ToList();

        // Đếm theo game trên tập đã lọc nguồn/ngày/từ khóa, để chip game cho biết còn bao nhiêu tin.
        var gameCounts = items.Where(i => i.GameCode != null).GroupBy(i => i.GameCode!).ToDictionary(g => g.Key, g => g.Count());
        var gameKey = (game ?? "all").Trim().ToUpperInvariant();
        if (gameKey != "ALL")
        {
            if (!games.Any(g => g.Code == gameKey)) return BadRequest(new { message = "Tựa game không hợp lệ." });
            items = items.Where(i => i.GameCode == gameKey).ToList();
        }

        return Ok(new
        {
            total = items.Count,
            page,
            pageSize,
            items = items.Skip((page - 1) * pageSize).Take(pageSize),
            sources,
            games = games.Select(g => new { g.Code, g.Title, g.LogoUrl, g.BannerUrl, Count = gameCounts.GetValueOrDefault(g.Code) })
        });
    }

    // Một tin báo (RSS) để đọc ngay trên ETOMS: tiêu đề, ảnh, đoạn mô tả báo cung cấp, nút đọc toàn bài ở báo gốc.
    [HttpGet("external/{id}")]
    public async Task<IActionResult> GetExternal(string id, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(id) || id.Length > 64) return BadRequest(new { message = "Mã tin không hợp lệ." });
        var item = await _news.GetExternalAsync(id, ct);
        if (item is null) return NotFound(new { message = "Tin này không còn trong danh sách tin mới (báo đã cập nhật). Vui lòng chọn tin khác." });
        // Tin khác: ưu tiên tin cùng game, rồi tin mới nhất.
        var related = (await _news.GetAllAsync(ct)).Items
            .Where(i => i.Id != item.Id)
            .OrderByDescending(i => item.GameCode != null && i.GameCode == item.GameCode)
            .Take(6)
            .Select(i => new { i.Title, i.SourceName, i.ReaderUrl, i.PublishedAt, i.ImageUrl, i.GameCode, i.IsInternal });
        return Ok(new
        {
            item.Id, item.SourceName, item.Title, item.Url, item.ImageUrl, item.PublishedAt, item.GameCode,
            Description = item.Description ?? item.Summary,
            Related = related
        });
    }

    // Bài tin nội bộ (Admin đăng).
    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken ct)
    {
        var item = await _news.GetInternalAsync(id, ct);
        if (item is null) return NotFound(new { message = "Không tìm thấy bài tin." });
        return Ok(new
        {
            item.ContentItemId,
            item.Title,
            Body = RichTextSanitizer.Sanitize(item.Body),
            ImageUrl = NewsFeedService.InternalImage(item.Body),
            item.PublishedAt,
            GameCode = NewsFeedService.DetectGame(item.Title),
            Author = item.AuthorAccount?.FullName
        });
    }
}
