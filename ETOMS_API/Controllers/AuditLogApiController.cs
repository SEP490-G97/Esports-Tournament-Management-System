using ETOMS_API.Models;
using ETOMS_API.Repositories;
using ETOMS_API.Services.Interface;
using ETOMS_API.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ETOMS_API.Controllers;

// UC-43 — đọc nhật ký (AuditLog). Quản trị viên đọc toàn hệ thống; Ban tổ chức đọc nhật ký của giải mình.
// "No role may edit or delete an audit log entry": controller này CHỈ có GET, không có thao tác sửa/xóa.
public sealed class AuditLogApiController : ApiControllerBase
{
    private const int MaxPageSize = 100;
    private readonly EtomsDbContext _db;
    private readonly ICurrentUser _currentUser;

    public AuditLogApiController(EtomsDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public static string ActionLabel(string action) => action switch
    {
        "FORCE_UPDATE" => "Cập nhật kết quả bắt buộc",
        "REMATCH" => "Hủy kết quả, đấu lại",
        "MANUAL_CHECKIN" => "Điểm danh thủ công",
        "DISQUALIFY" => "Tước quyền thi đấu",
        "BAN" => "Khóa tài khoản",
        "UNBAN" => "Mở khóa tài khoản",
        "APPROVE" => "Duyệt",
        "REJECT" => "Từ chối",
        "RULEBOOK_EDIT" => "Sửa thể lệ",
        "CANCEL_TOURNAMENT" => "Hủy giải",
        "GRANT_ORGANIZER" => "Cấp quyền Ban tổ chức",
        "LOCK_BRACKET" => "Khóa nhánh đấu",
        "WALKOVER" => "Xử thắng trắng",
        "AUTO_VERIFY" => "Tự xác nhận kết quả",
        "ISSUE_PENALTY" => "Cấp thẻ phạt",
        "CANCEL_PENALTY" => "Hủy thẻ phạt",
        "SUSPEND_ORGANIZATION" => "Đình chỉ đơn vị tổ chức",
        "TRANSFER_CHAIR" => "Chuyển quyền chủ tịch đơn vị",
        "PROMOTE_WAITLIST" => "Đôn đội từ danh sách chờ",
        "APPEAL_DECISION" => "Quyết định khiếu nại",
        "VERIFY_PAYMENT" => "Xác nhận lệ phí",
        "REFUND_RECORDED" => "Ghi nhận hoàn phí",
        "GAME_CREATE" => "Thêm tựa game",
        "GAME_UPDATE" => "Sửa tựa game",
        "GAME_STATUS" => "Đổi trạng thái tựa game",
        "GAME_DELETE" => "Xóa tựa game",
        "ANNOUNCE_SEND" => "Gửi thông báo hệ thống",
        "BANNER_CREATE" => "Thêm biểu ngữ",
        "BANNER_UPDATE" => "Sửa biểu ngữ",
        "BANNER_DELETE" => "Xóa biểu ngữ",
        "BANNER_REORDER" => "Sắp xếp biểu ngữ",
        _ => action
    };

    private static readonly string[] KnownActions =
    {
        "FORCE_UPDATE", "REMATCH", "MANUAL_CHECKIN", "DISQUALIFY", "BAN", "UNBAN", "APPROVE", "REJECT", "RULEBOOK_EDIT",
        "CANCEL_TOURNAMENT", "GRANT_ORGANIZER", "LOCK_BRACKET", "WALKOVER", "AUTO_VERIFY", "ISSUE_PENALTY", "CANCEL_PENALTY",
        "SUSPEND_ORGANIZATION", "TRANSFER_CHAIR", "PROMOTE_WAITLIST", "APPEAL_DECISION", "VERIFY_PAYMENT", "REFUND_RECORDED",
        "GAME_CREATE", "GAME_UPDATE", "GAME_STATUS", "GAME_DELETE", "ANNOUNCE_SEND", "BANNER_CREATE", "BANNER_UPDATE",
        "BANNER_DELETE", "BANNER_REORDER"
    };

    // GET /api/admin/audit-logs — Quản trị viên: lọc theo giải, loại thao tác, từ khóa người thực hiện, khoảng ngày.
    [HttpGet("api/admin/audit-logs")]
    public async Task<IActionResult> All([FromQuery] int? tournamentId, [FromQuery] string? actionType, [FromQuery] string? keyword,
        [FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
    {
        if (!_currentUser.IsAuthenticated) return Unauthorized();
        if (!User.IsInRole(AccountRoles.Admin))
            return FromResult(Result.Failure("Chỉ quản trị viên mới xem được nhật ký toàn hệ thống.", StatusCodes.Status403Forbidden));
        if ((keyword?.Length ?? 0) > 100) return FromResult(Result.FieldFailure(nameof(keyword), "Bộ lọc không hợp lệ."));   // MSG-ADM-001
        return await QueryAsync(tournamentId, actionType, keyword, from, to, page, pageSize, true, ct);
    }

    // GET /api/tournaments/{id}/audit-log — Ban tổ chức của giải (hoặc quản trị viên) đọc nhật ký của giải.
    [HttpGet("api/tournaments/{tournamentId:int}/audit-log")]
    public async Task<IActionResult> OfTournament(int tournamentId, [FromQuery] string? actionType,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
    {
        if (!_currentUser.IsAuthenticated) return Unauthorized();
        var t = await _db.Tournaments.AsNoTracking().Where(x => x.TournamentId == tournamentId)
            .Select(x => new { x.OrganizerAccountId }).FirstOrDefaultAsync(ct);
        if (t is null) return FromResult(Result.Failure("Không tìm thấy giải đấu.", StatusCodes.Status404NotFound));
        if (t.OrganizerAccountId != _currentUser.UserId && !User.IsInRole(AccountRoles.Admin))
            return FromResult(Result.Failure("Chỉ ban tổ chức của giải mới xem được nhật ký của giải.", StatusCodes.Status403Forbidden));
        return await QueryAsync(tournamentId, actionType, null, null, null, page, pageSize, false, ct);
    }

    private async Task<IActionResult> QueryAsync(int? tournamentId, string? actionType, string? keyword, DateTime? from, DateTime? to,
        int page, int pageSize, bool admin, CancellationToken ct)
    {
        var action = (actionType ?? "").Trim().ToUpperInvariant();
        if (action.Length > 0 && !KnownActions.Contains(action))
            return FromResult(Result.FieldFailure(nameof(actionType), "Bộ lọc không hợp lệ."));
        if (from is not null && to is not null && from > to)
            return FromResult(Result.FieldFailure(nameof(from), "Bộ lọc không hợp lệ."));
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        IQueryable<AuditLog> q = _db.AuditLogs.AsNoTracking();
        if (tournamentId is int tid) q = q.Where(a => a.TournamentId == tid);
        if (action.Length > 0) q = q.Where(a => a.ActionType == action);
        var kw = (keyword ?? "").Trim();
        if (kw.Length > 0) q = q.Where(a => a.ActorAccount != null && (a.ActorAccount.FullName.Contains(kw) || a.ActorAccount.Email.Contains(kw)));
        if (from is DateTime f) q = q.Where(a => a.CreatedAt >= f);
        if (to is DateTime tt) q = q.Where(a => a.CreatedAt < tt.Date.AddDays(1));

        var total = await q.CountAsync(ct);
        var rows = await q.OrderByDescending(a => a.CreatedAt).ThenByDescending(a => a.AuditLogId)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(a => new
            {
                a.AuditLogId, a.CreatedAt, a.ActionType, a.ObjectType, a.ObjectId, a.BeforeValue, a.AfterValue, a.Reason,
                a.TournamentId,
                TournamentName = a.Tournament != null ? a.Tournament.Name : null,
                ActorName = a.ActorAccount != null ? a.ActorAccount.FullName : null,
                ActorEmail = a.ActorAccount != null ? a.ActorAccount.Email : null
            }).ToListAsync(ct);

        Response.Headers.CacheControl = "no-store";
        return FromResult(Result.Success(new
        {
            Total = total,
            Page = page,
            PageSize = pageSize,
            Actions = KnownActions.Select(x => new { Code = x, Label = ActionLabel(x) }),
            Items = rows.Select(a => new
            {
                a.AuditLogId, a.CreatedAt, a.ActionType, ActionLabel = ActionLabel(a.ActionType), a.ObjectType, a.ObjectId,
                a.BeforeValue, a.AfterValue, a.Reason, a.TournamentId, a.TournamentName,
                ActorName = a.ActorName ?? "Hệ thống (tự động)",
                ActorEmail = admin ? a.ActorEmail : null
            })
        }));
    }
}
