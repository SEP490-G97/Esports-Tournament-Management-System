using ETOMS_API.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ETOMS_API.Controllers;

/// <summary>Banner tổng đang hiển thị — công khai để trang chủ dùng.</summary>
[ApiController]
[AllowAnonymous]
[Route("api/banners")]
public class BannersController : ControllerBase
{
    private readonly IAdminBannerService _svc;

    public BannersController(IAdminBannerService svc) => _svc = svc;

    [HttpGet]
    public async Task<IActionResult> Live(CancellationToken ct)
    {
        var items = await _svc.GetLiveAsync(ct);
        Response.Headers.CacheControl = "public, max-age=60";
        return Ok(items);
    }
}