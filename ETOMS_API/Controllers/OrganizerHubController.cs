using ETOMS_API.Services.Impl;
using ETOMS_API.Services.Interface;
using ETOMS_API.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ETOMS_API.Controllers;

// GET /api/organizer/tournaments — trang "Giải của tôi": giải của chính Ban tổ chức đang đăng nhập.
[ApiController]
[Route("api/organizer/tournaments")]
[Authorize(Roles = AccountRoles.Organizer)]
public sealed class OrganizerHubController : ControllerBase
{
    private readonly OrganizerHubService _svc;
    private readonly ICurrentUser _me;

    public OrganizerHubController(OrganizerHubService svc, ICurrentUser me)
    {
        _svc = svc;
        _me = me;
    }

    [HttpGet]
    public async Task<IActionResult> List() => Ok(await _svc.ListAsync(_me.UserId!.Value));
}
