using ETOMS_API.Services.Interface;
using ETOMS_API.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ETOMS_API.Controllers
{
    public record BanRequest(string Reason);

    [ApiController, Route("api/admin/accounts"), Authorize(Roles = AccountRoles.Admin)]
    public class AdminAccountsController(IAccountBanService bans) : ControllerBase
    {
        [HttpPost("{id:int}/ban")]
        public async Task<IActionResult> Ban(int id, BanRequest req, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(req.Reason))
                return BadRequest(new { message = "Phải nhập lý do khóa tài khoản" });

            return await bans.BanAsync(id, req.Reason.Trim(), ct)
                ? Ok(new { message = "Đã khóa tài khoản" })
                : NotFound(new { message = "Không tìm thấy tài khoản" });
        }

        [HttpPost("{id:int}/unban")]
        public async Task<IActionResult> Unban(int id, CancellationToken ct) =>
            await bans.UnbanAsync(id, ct)
                ? Ok(new { message = "Đã mở khóa tài khoản" })
                : NotFound(new { message = "Không tìm thấy tài khoản" });
    }
}
