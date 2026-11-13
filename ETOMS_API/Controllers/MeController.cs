using ETOMS_API.Repositories.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using static ETOMS_API.DTOs.Auth.AuthDtos;

namespace ETOMS_API.Controllers
{
    [ApiController, Route("api"), Authorize]
    public class MeController(IAccountRepository accounts) : ControllerBase
    {
        [HttpGet("me")]
        public async Task<IActionResult> Me(CancellationToken ct)
        {
            var id = int.Parse(User.FindFirst("sub")!.Value);
            var a = await accounts.FindByIdAsync(id, ct);
            return a is null
                ? Unauthorized()
                : Ok(new AccountDto(a.AccountId, a.Email, a.FullName, a.AvatarUrl, a.Role));
        }
    }
}
