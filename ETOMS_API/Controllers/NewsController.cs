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

    // ?source=all|etoms|gamek|tuoitre|thanhnien &q=từ khóa &page=1 &pageSize=12
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? source, [FromQuery] string? q,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 12, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 30);
        var (items, sources) = await _news.GetAllAsync(ct);

        var key = (source ?? "all").Trim().ToLowerInvariant();
        if (key != "all")
        {
            if (!sources.Any(s => s.Key == key)) return BadRequest(new { message = "Nguồn tin không hợp lệ." });
            items = items.Where(i => i.Source == key).ToList();
        }
        var term = (q ?? string.Empty).Trim();
        if (term.Length > 100) return BadRequest(new { message = "Từ khóa tối đa 100 ký tự." });
        if (term.Length > 0)
            items = items.Where(i => i.Title.Contains(term, StringComparison.CurrentCultureIgnoreCase)
                || (i.Summary ?? string.Empty).Contains(term, StringComparison.CurrentCultureIgnoreCase)).ToList();

        return Ok(new
        {
            total = items.Count,
            page,
            pageSize,
            items = items.Skip((page - 1) * pageSize).Take(pageSize),
            sources
        });
    }

    // Bài tin nội bộ (Admin đăng) — tin ngoài mở thẳng bài gốc trên trang báo.
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
            item.PublishedAt,
            Author = item.AuthorAccount?.FullName
        });
    }
}
