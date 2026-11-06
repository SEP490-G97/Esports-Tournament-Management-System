using ETOMS_API.Models;
using ETOMS_API.Repositories;
using ETOMS_API.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ETOMS_API.Controllers;

// ====== STUB TẠM — KHÔNG PHẢI AUTH THẬT ======
// Bản JSON của DevAuthController (MVC) — dùng cho các trang HTML tĩnh gọi fetch() để
// chọn nhanh một tài khoản demo test (TEAM...) trong khi module AUTH (Vũ Đức Hải) chưa xong.
// Khi có AUTH thật: xóa controller này.
[Route("api/dev-auth")]
public class DevAuthApiController : ApiControllerBase
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public DevAuthApiController(IUnitOfWork uow, ICurrentUser currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    // GET /api/dev-auth/accounts — danh sách tài khoản demo để chọn đăng nhập.
    [HttpGet("accounts")]
    public async Task<IActionResult> GetAccounts()
    {
        var users = await _uow.Repository<Account>().Query()
            .Where(u => u.Status == "ACTIVE")
            .OrderBy(u => u.AccountId)
            .Select(u => new { u.AccountId, u.FullName, u.Email })
            .ToListAsync();
        return Ok(users);
    }

    // POST /api/dev-auth/login — ghi session, không redirect (client tự điều hướng).
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] DevAuthLoginRequest body)
    {
        var user = await _uow.Repository<Account>().GetByIdAsync(body.AccountId);
        if (user is null) return NotFound();

        HttpContext.Session.SetInt32("DemoUserId", user.AccountId);
        HttpContext.Session.SetString("DemoUserName", user.FullName);

        return Ok(new { message = $"Đăng nhập thành công: {user.FullName}", data = new { accountId = user.AccountId, fullName = user.FullName } });
    }

    // POST /api/dev-auth/logout
    [HttpPost("logout")]
    public IActionResult Logout()
    {
        HttpContext.Session.Remove("DemoUserId");
        HttpContext.Session.Remove("DemoUserName");
        return Ok(new { message = "Đã đăng xuất." });
    }

    // GET /api/dev-auth/me — ai đang đăng nhập (dùng cho widget nav + check captain/member phía client,
    // và để ẩn/hiện mục menu theo Role — PLAYER/ORGANIZER/ADMIN).
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        string? role = null;
        if (_currentUser.IsAuthenticated)
        {
            var account = await _uow.Repository<Account>().GetByIdAsync(_currentUser.UserId!.Value);
            role = account?.Role;
        }
        return Ok(new { isAuthenticated = _currentUser.IsAuthenticated, userId = _currentUser.UserId, displayName = _currentUser.DisplayName, role });
    }
}

public class DevAuthLoginRequest { public int AccountId { get; set; } }
