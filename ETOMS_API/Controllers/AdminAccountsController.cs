
using ETOMS_API.DTOs.Admin;
using ETOMS_API.Services;
using ETOMS_API.Services.Impl;
using ETOMS_API.Services.Interface;
using ETOMS_API.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ETOMS_API.Controllers;

/// <summary>ADM-01 (danh sách/tìm kiếm) và ADM-02 (khóa/mở khóa). Thay thế bản cũ chỉ có ban/unban.</summary>
[ApiController]
[Route("api/admin/accounts")]
[Authorize(Roles = AccountRoles.Admin)]
public sealed class AdminAccountsController : ControllerBase
{
    private readonly IAdminAccountService _svc;

    public AdminAccountsController(IAdminAccountService svc) => _svc = svc;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] AccountListQuery query, CancellationToken ct)
    {
        var r = await _svc.ListAsync(query, ct);
        return r.Ok ? Ok(r.Data) : Fail(r.Error);
    }

    [HttpPost("{id:int}/ban")]
    public async Task<IActionResult> Ban(int id, [FromBody] BanAccountRequest? body, CancellationToken ct)
    {
        if (!TryGetActorId(out var actorId)) return Unauthorized();
        var r = await _svc.BanAsync(actorId, id, body?.Reason, ct);
        return r.Ok ? Ok(new { message = "Đã khóa tài khoản." }) : Fail(r.Error);
    }

    [HttpPost("{id:int}/unban")]
    public async Task<IActionResult> Unban(int id, [FromBody] UnbanAccountRequest? body, CancellationToken ct)
    {
        if (!TryGetActorId(out var actorId)) return Unauthorized();
        var r = await _svc.UnbanAsync(actorId, id, body?.Note, ct);
        return r.Ok ? Ok(new { message = "Đã mở khóa tài khoản." }) : Fail(r.Error);
    }

    private bool TryGetActorId(out int id)
    {
        // Program.cs đặt MapInboundClaims = false nên claim vẫn tên "sub".
        var raw = User.FindFirst("sub")?.Value;
        return int.TryParse(raw, out id);
    }

    internal static IActionResult MapError(AdminError e) => e switch
    {
        AdminError.InvalidReason => Body(400, "REASON_INVALID",
            $"Lý do phải từ {AdminAccountService.MinReasonLength} đến {AdminAccountService.MaxReasonLength} ký tự."),
        AdminError.InvalidFilter => Body(400, "INVALID_FILTER", "Bộ lọc không hợp lệ."),
        AdminError.NotFound => Body(404, "ACCOUNT_NOT_FOUND", "Không tìm thấy tài khoản."),
        AdminError.CannotBanSelf => Body(403, "CANNOT_BAN_SELF", "Bạn không thể tự khóa tài khoản của mình."),
        AdminError.CannotBanAdmin => Body(403, "CANNOT_BAN_ADMIN", "Không thể khóa tài khoản quản trị viên."),
        AdminError.AlreadyBanned => Body(409, "ALREADY_BANNED", "Tài khoản này đã bị khóa."),
        AdminError.NotBanned => Body(409, "NOT_BANNED", "Tài khoản này hiện không bị khóa."),
        _ => Body(500, "SERVER_ERROR", "Đã xảy ra lỗi.")
    };

    private IActionResult Fail(AdminError e) => MapError(e);

    private static ObjectResult Body(int status, string code, string message)
        => new(new { code, message }) { StatusCode = status };
}