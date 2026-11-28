using ETOMS_API.DTOs.Admin;
using ETOMS_API.Services.Interface;
using ETOMS_API.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ETOMS_API.Controllers;

// UC-43: tuyển thủ gửi đơn xin làm Ban tổ chức (kèm mong muốn) — chỉ chính chủ.
[Route("api/me/organizer-request")]
[Authorize]
public sealed class MyOrganizerRequestController : ApiControllerBase
{
    private readonly IOrganizerRequestService _svc;
    private readonly ICurrentUser _me;

    public MyOrganizerRequestController(IOrganizerRequestService svc, ICurrentUser me)
    {
        _svc = svc;
        _me = me;
    }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var dto = await _svc.GetMineAsync(_me.UserId!.Value, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpPost]
    public async Task<IActionResult> Submit([FromBody] SubmitOrganizerRequest request, CancellationToken ct)
        => FromResult(await _svc.SubmitAsync(_me.UserId!.Value, request, ct));

    [HttpDelete]
    public async Task<IActionResult> Cancel(CancellationToken ct)
        => FromResult(await _svc.CancelAsync(_me.UserId!.Value, ct));

    // Đơn đã duyệt: xem mật khẩu tài khoản Ban tổ chức đúng MỘT lần (góp ý 28/11). Phản hồi có mật khẩu nên không lưu đệm.
    [HttpPost("organizer-password/reveal")]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("login")]
    public async Task<IActionResult> RevealOrganizerPassword(CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        return FromResult(await _svc.RevealOrganizerPasswordAsync(_me.UserId!.Value, ct));
    }

    // Đã xem rồi mà cần xem lại: gửi yêu cầu, Admin xác nhận thì được xem mật khẩu mới thêm một lần.
    [HttpPost("organizer-password/reissue")]
    public async Task<IActionResult> RequestReissue(CancellationToken ct)
        => FromResult(await _svc.RequestPasswordReissueAsync(_me.UserId!.Value, ct));
}

// UC-43: quản trị viên xét đơn — duyệt = cấp tài khoản Ban tổ chức RIÊNG; từ chối phải ghi lý do.
[Route("api/admin/organizer-requests")]
[Authorize(Roles = AccountRoles.Admin)]
public sealed class AdminOrganizerRequestsController : ApiControllerBase
{
    private readonly IOrganizerRequestService _svc;
    private readonly ICurrentUser _me;

    public AdminOrganizerRequestsController(IOrganizerRequestService svc, ICurrentUser me)
    {
        _svc = svc;
        _me = me;
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(await _svc.ListPendingAsync(ct));

    [HttpPost("{accountId:int}/approve")]
    public async Task<IActionResult> Approve(int accountId, [FromBody] ApproveOrganizerRequest request, CancellationToken ct)
        => FromResult(await _svc.ApproveAsync(_me.UserId!.Value, accountId, request, ct));

    // Yêu cầu xem lại mật khẩu Ban tổ chức đang chờ Admin xác nhận.
    [HttpGet("reissues")]
    public async Task<IActionResult> ListReissues(CancellationToken ct) => Ok(await _svc.ListReissueRequestsAsync(ct));

    [HttpPost("{accountId:int}/reissue-approve")]
    public async Task<IActionResult> ApproveReissue(int accountId, CancellationToken ct)
        => FromResult(await _svc.ApprovePasswordReissueAsync(_me.UserId!.Value, accountId, ct));

    [HttpPost("{accountId:int}/reject")]
    public async Task<IActionResult> Reject(int accountId, [FromBody] RejectOrganizerRequest request, CancellationToken ct)
        => FromResult(await _svc.RejectAsync(_me.UserId!.Value, accountId, request, ct));
}
