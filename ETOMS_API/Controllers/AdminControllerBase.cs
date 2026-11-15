using ETOMS_API.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ETOMS_API.Controllers
{
    [ApiController]
    [Authorize(Roles = AdminConstants.AdminRole)]
    public abstract class AdminControllerBase : ControllerBase
    {
        /// <summary>Lấy id người thao tác từ claim "sub" (MapInboundClaims = false).</summary>
        protected bool TryGetActorId(out int actorId)
        {
            var raw = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(raw, out actorId) && actorId > 0;
        }

        protected IActionResult Err(int status, string code, string message) =>
            StatusCode(status, new { code, message });
    }
}
