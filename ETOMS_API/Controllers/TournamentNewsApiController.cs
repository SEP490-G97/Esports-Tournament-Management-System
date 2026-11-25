using ETOMS_API.Models;
using ETOMS_API.Repositories;
using ETOMS_API.Services.Interface;
using ETOMS_API.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ETOMS_API.Controllers;

// UC-13 A4 — Ban tổ chức đăng tin cho giải (bảng có sẵn ContentItem: Kind = NEWS, TournamentID = giải). Tin đã đăng hiện
// ở trang giải và trang Tin tức (NewsFeedService đọc mọi tin NEWS đã đăng). Không xóa tin: chọn "Ẩn" để gỡ khỏi trang.
// "Báo cho người theo dõi giải" của A4 không làm được: bảng TournamentFollow đã bỏ khỏi lược đồ 35 bảng (nhóm chốt 18/11).
[Route("api/tournaments/{tournamentId:int}/news")]
public sealed class TournamentNewsApiController : ApiControllerBase
{
    private static readonly string[] Statuses = { "DRAFT", "PUBLISHED", "HIDDEN" };
    private readonly EtomsDbContext _db;
    private readonly ICurrentUser _currentUser;

    public TournamentNewsApiController(EtomsDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public sealed record TournamentNewsRequest(string? Title, string? Body, string? Status);

    // Công khai: tin đã đăng của giải không phải nháp. Ban tổ chức của giải: mọi tin (kể cả nháp / ẩn).
    [HttpGet]
    public async Task<IActionResult> List(int tournamentId, CancellationToken ct)
    {
        var t = await _db.Tournaments.AsNoTracking().Where(x => x.TournamentId == tournamentId)
            .Select(x => new { x.OrganizerAccountId, x.Status, x.Name }).FirstOrDefaultAsync(ct);
        var isOrganizer = t is not null && _currentUser.IsAuthenticated && t.OrganizerAccountId == _currentUser.UserId;
        if (t is null || (t.Status == "DRAFT" && !isOrganizer))
            return FromResult(Result.Failure("Không tìm thấy giải đấu.", StatusCodes.Status404NotFound));
        var rows = await _db.ContentItems.AsNoTracking()
            .Where(c => c.Kind == "NEWS" && c.TournamentId == tournamentId && (isOrganizer || c.Status == "PUBLISHED"))
            .OrderByDescending(c => c.PublishedAt ?? c.UpdatedAt)
            .Select(c => new { c.ContentItemId, c.Title, c.Body, c.Status, c.PublishedAt, c.UpdatedAt })
            .ToListAsync(ct);
        return FromResult(Result.Success(new { TournamentName = t.Name, IsOrganizer = isOrganizer, Items = rows }));
    }

    [HttpPost]
    public Task<IActionResult> Create(int tournamentId, [FromBody] TournamentNewsRequest body, CancellationToken ct) => SaveAsync(tournamentId, null, body, ct);

    [HttpPut("{id:int}")]
    public Task<IActionResult> Update(int tournamentId, int id, [FromBody] TournamentNewsRequest body, CancellationToken ct) => SaveAsync(tournamentId, id, body, ct);

    private async Task<IActionResult> SaveAsync(int tournamentId, int? id, TournamentNewsRequest? body, CancellationToken ct)
    {
        if (!_currentUser.IsAuthenticated) return Unauthorized();
        var t = await _db.Tournaments.AsNoTracking().FirstOrDefaultAsync(x => x.TournamentId == tournamentId, ct);
        if (t is null) return FromResult(Result.Failure("Không tìm thấy giải đấu.", StatusCodes.Status404NotFound));
        if (t.OrganizerAccountId != _currentUser.UserId)
            return FromResult(Result.Failure("Chỉ ban tổ chức của giải mới đăng được tin cho giải.", StatusCodes.Status403Forbidden));

        var title = (body?.Title ?? "").Trim();
        var text = (body?.Body ?? "").Trim();
        var status = (body?.Status ?? "PUBLISHED").Trim().ToUpperInvariant();
        if (title.Length == 0 || text.Length == 0)
            return FromResult(Result.FieldFailure(title.Length == 0 ? "Title" : "Body", "Vui lòng nhập đầy đủ thông tin bắt buộc."));   // E5, MSG-COM-002
        if (title.Length > 200) return FromResult(Result.FieldFailure("Title", "Tiêu đề tối đa 200 ký tự."));
        if (text.Length > 20000) return FromResult(Result.FieldFailure("Body", "Nội dung tin tối đa 20.000 ký tự."));
        if (!Statuses.Contains(status)) return FromResult(Result.FieldFailure("Status", "Trạng thái tin không hợp lệ."));

        ContentItem? item;
        if (id is int nid)
        {
            item = await _db.ContentItems.FirstOrDefaultAsync(c => c.ContentItemId == nid && c.Kind == "NEWS" && c.TournamentId == tournamentId, ct);
            if (item is null) return FromResult(Result.Failure("Không tìm thấy tin của giải.", StatusCodes.Status404NotFound));
            if (status != "PUBLISHED") item.IsPinned = false;   // CK_ContentItem_PinShow: chỉ tin đã đăng mới được ghim
        }
        else
        {
            item = new ContentItem { Kind = "NEWS", TournamentId = tournamentId, AuthorAccountId = _currentUser.UserId!.Value, IsPinned = false };
            _db.ContentItems.Add(item);
        }
        var now = DateTime.UtcNow;
        item.Title = title;
        item.Body = text;
        item.Status = status;
        if (status == "PUBLISHED") item.PublishedAt ??= now;
        item.UpdatedAt = now;
        await _db.SaveChangesAsync(ct);
        return FromResult(Result.Success(new { item.ContentItemId },
            status == "PUBLISHED" ? "Đã đăng tin của giải." : status == "DRAFT" ? "Đã lưu tin nháp." : "Đã ẩn tin."));
    }
}
