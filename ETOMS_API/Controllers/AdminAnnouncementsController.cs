using ETOMS_API.DTOs.Admin;
using ETOMS_API.Services.Interface;
using Microsoft.AspNetCore.Mvc;

namespace ETOMS_API.Controllers;

[Route("api/admin/announcements")]
public class AdminAnnouncementsController : AdminControllerBase
{
    private readonly IAdminAnnouncementService _svc;

    public AdminAnnouncementsController(IAdminAnnouncementService svc) => _svc = svc;

    /// <summary>GET /api/admin/announcements?page=1&amp;pageSize=20 — lịch sử các lần phát.</summary>
    [HttpGet]
    public async Task<IActionResult> History([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => Ok(await _svc.HistoryAsync(page, pageSize, ct));

    /// <summary>POST /api/admin/announcements/audience — đếm người sẽ nhận (cùng body với gửi; Title/Body không cần).</summary>
    [HttpPost("audience")]
    public async Task<IActionResult> Audience([FromBody] SendAnnouncementRequest request, CancellationToken ct)
    {
        var r = await _svc.CountAudienceAsync(request ?? new SendAnnouncementRequest(), ct);
        return r.Error == AnnouncementError.None ? Ok(r.Data) : Map(r.Error);
    }

    [HttpPost]
    public async Task<IActionResult> Send([FromBody] SendAnnouncementRequest request, CancellationToken ct)
    {
        if (!TryGetActorId(out var actor)) return Unauthorized();
        var r = await _svc.SendAsync(actor, request, ct);
        return r.Error == AnnouncementError.None ? StatusCode(201, r.Data) : Map(r.Error);
    }

    private IActionResult Map(AnnouncementError e) => e switch
    {
        AnnouncementError.InvalidTitle => Err(400, "TITLE_INVALID", "Tiêu đề phải từ 3 đến 200 ký tự."),
        AnnouncementError.InvalidBody => Err(400, "BODY_INVALID", "Nội dung phải từ 5 đến 1000 ký tự."),
        AnnouncementError.InvalidRoles => Err(400, "ROLES_INVALID", "Nhóm người nhận không hợp lệ."),
        AnnouncementError.InvalidChannel => Err(400, "CHANNEL_INVALID", "Kênh gửi phải là IN_APP, EMAIL hoặc BOTH."),
        AnnouncementError.InvalidLink => Err(400, "LINK_INVALID",
            "Liên kết phải là đường dẫn nội bộ bắt đầu bằng \"/\" hoặc URL https."),
        AnnouncementError.InvalidEmails => Err(400, "EMAILS_INVALID", "Có địa chỉ email không hợp lệ."),
        AnnouncementError.TooManyEmails => Err(400, "EMAILS_TOO_MANY", "Quá nhiều email (tối đa 200 email chỉ định, 1000 email loại trừ)."),
        AnnouncementError.NoRecipients => Err(409, "NO_RECIPIENTS",
            "Không có người nhận nào (không có tài khoản ACTIVE bật nhận thông báo qua kênh đã chọn trong nhóm đã chọn)."),
        AnnouncementError.DuplicateRecent => Err(409, "DUPLICATE_RECENT",
            "Bạn vừa gửi một thông báo cùng tiêu đề trong vòng 1 phút. Hãy kiểm tra lịch sử trước khi gửi lại."),
        _ => Err(500, "UNKNOWN", "Lỗi không xác định.")
    };
}