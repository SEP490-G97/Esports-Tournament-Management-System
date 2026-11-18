using ETOMS_API.Models;
using ETOMS_API.Repositories;
using System.Security.Claims;
using ETOMS_API.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ETOMS_API.Controllers;

// Quản lý tin tức ETOMS (góp ý nhóm 18/11 mục 20). Dùng bảng có sẵn ContentItem (Kind = NEWS) — không thêm bảng.
// Tin đã đăng hiện ở trang chủ + trang Tin tức (NewsFeedService / HomeApiController đọc Status = PUBLISHED).
// Ràng buộc theo CHECK của bảng: chỉ ghim tin đã đăng; tin đã đăng phải có PublishedAt; Status DRAFT | PUBLISHED | HIDDEN.
// Không xóa tin: "Ẩn" (HIDDEN) để giữ lịch sử.
[ApiController]
[Route("api/admin/news")]
[Authorize(Roles = AccountRoles.Admin)]
public sealed class AdminNewsController : ControllerBase
{
    private bool TryGetActorId(out int actorId)
    {
        var raw = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(raw, out actorId) && actorId > 0;
    }

    private ObjectResult Err(int status, string code, string message) => StatusCode(status, new { code, message });

    private static readonly string[] Statuses = { "DRAFT", "PUBLISHED", "HIDDEN" };
    private readonly EtomsDbContext _db;

    public AdminNewsController(EtomsDbContext db) => _db = db;

    public sealed record NewsRequest(string? Title, string? Body, string? Status, bool IsPinned);

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var rows = await _db.ContentItems.AsNoTracking().Where(c => c.Kind == "NEWS")
            .OrderByDescending(c => c.IsPinned).ThenByDescending(c => c.PublishedAt ?? c.UpdatedAt)
            .Select(c => new { c.ContentItemId, c.Title, c.Body, c.Status, c.IsPinned, c.PublishedAt, c.UpdatedAt, Author = c.AuthorAccount.FullName })
            .ToListAsync(ct);
        Response.Headers.CacheControl = "no-store";
        return Ok(rows);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] NewsRequest body, CancellationToken ct)
    {
        if (!TryGetActorId(out var actor)) return Unauthorized();
        if (Validate(body) is { } bad) return bad;
        var now = DateTime.UtcNow;
        var status = body.Status!.Trim().ToUpperInvariant();
        var item = new ContentItem
        {
            Kind = "NEWS",
            Title = body.Title!.Trim(),
            Body = body.Body!.Trim(),
            Status = status,
            IsPinned = status == "PUBLISHED" && body.IsPinned,
            AuthorAccountId = actor,
            PublishedAt = status == "PUBLISHED" ? now : null,
            UpdatedAt = now
        };
        _db.ContentItems.Add(item);
        await _db.SaveChangesAsync(ct);
        return Ok(new { message = status == "PUBLISHED" ? "Đã đăng tin." : "Đã lưu tin nháp.", item.ContentItemId });
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] NewsRequest body, CancellationToken ct)
    {
        if (Validate(body) is { } bad) return bad;
        var item = await _db.ContentItems.FirstOrDefaultAsync(c => c.ContentItemId == id && c.Kind == "NEWS", ct);
        if (item is null) return Err(404, "NEWS_NOT_FOUND", "Không tìm thấy tin.");
        var status = body.Status!.Trim().ToUpperInvariant();
        item.Title = body.Title!.Trim();
        item.Body = body.Body!.Trim();
        item.Status = status;
        item.IsPinned = status == "PUBLISHED" && body.IsPinned;
        if (status == "PUBLISHED") item.PublishedAt ??= DateTime.UtcNow;   // đăng lần đầu mới đặt ngày đăng
        item.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Ok(new { message = "Đã lưu tin." });
    }

    private IActionResult? Validate(NewsRequest? b)
    {
        var title = (b?.Title ?? "").Trim();
        var text = (b?.Body ?? "").Trim();
        var status = (b?.Status ?? "").Trim().ToUpperInvariant();
        if (title.Length < 5 || title.Length > 200) return Err(400, "TITLE_INVALID", "Tiêu đề từ 5 đến 200 ký tự.");
        if (text.Length < 20) return Err(400, "BODY_INVALID", "Nội dung tin tối thiểu 20 ký tự.");
        if (text.Length > 20000) return Err(400, "BODY_INVALID", "Nội dung tin tối đa 20.000 ký tự.");
        if (!Statuses.Contains(status)) return Err(400, "STATUS_INVALID", "Trạng thái tin không hợp lệ.");
        return null;
    }
}
