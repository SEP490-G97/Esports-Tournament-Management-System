using ETOMS.Application.Interfaces;
using ETOMS.Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ETOMS.Controllers;

// ====== STUB TẠM — KHÔNG PHẢI AUTH THẬT ======
// Chọn nhanh một tài khoản demo để test các chức năng cần đăng nhập (TEAM...) trong
// khi module AUTH (Vũ Đức Hải) chưa xong. Khi có AUTH thật: xóa controller + view này.
public class DevAuthController : Controller
{
    private readonly IUnitOfWork _uow;

    public DevAuthController(IUnitOfWork uow) => _uow = uow;

    [HttpGet]
    public async Task<IActionResult> Index(string? returnUrl)
    {
        ViewBag.ReturnUrl = returnUrl;
        var users = await _uow.Repository<Account>().Query()
            .Where(u => u.Status == "ACTIVE")
            .OrderBy(u => u.AccountId)
            .Select(u => new { u.AccountId, u.FullName, u.Email })
            .ToListAsync();
        return View(users.Select(u => (u.AccountId, u.FullName, u.Email)).ToList());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(int accountId, string? returnUrl)
    {
        var user = await _uow.Repository<Account>().GetByIdAsync(accountId);
        if (user is null) return NotFound();

        HttpContext.Session.SetInt32("DemoUserId", user.AccountId);
        HttpContext.Session.SetString("DemoUserName", user.FullName);

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);
        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    public IActionResult Logout()
    {
        HttpContext.Session.Remove("DemoUserId");
        HttpContext.Session.Remove("DemoUserName");
        return RedirectToAction("Index", "Home");
    }
}
