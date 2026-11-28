using System.Text.RegularExpressions;
using ETOMS_API.Services.Interface;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using static ETOMS_API.DTOs.Auth.AuthDtos;

namespace ETOMS_API.Controllers
{
    [ApiController, Route("auth")]
    public partial class AuthController(IAuthService auth, IWebHostEnvironment env) : ControllerBase
    {
        private const string CookieName = "refresh_token";
        private const string TabHeader = "X-Etoms-Tab";

        // Phiên riêng từng tab (góp ý 28/11): bỏ "Ghi nhớ đăng nhập" thì trang gửi kèm mã tab (header X-Etoms-Tab),
        // refresh token nằm ở cookie RIÊNG của tab đó ("refresh_token_<mã tab>"). Tab mới (kể cả mọi tab ẩn danh) không
        // có mã tab => không đọc được phiên của tab khác => chưa đăng nhập; mỗi tab đăng nhập được một tài khoản khác.
        // Không có header => cookie chung "refresh_token" (Ghi nhớ đăng nhập: dùng chung mọi tab như trước).
        private string RefreshCookieName()
        {
            var tab = Request.Headers[TabHeader].ToString();
            return TabId().IsMatch(tab) ? CookieName + "_" + tab : CookieName;
        }

        [GeneratedRegex("^[a-f0-9]{16,32}$")]
        private static partial Regex TabId();

        [HttpPost("login"), EnableRateLimiting("login")]
        public async Task<IActionResult> Login(LoginRequest req, CancellationToken ct) =>
            await LoginResponseAsync(await auth.LoginAsync(req.Email, req.Password, req.Remember, ct), ct);

        [HttpPost("google")]
        public async Task<IActionResult> Google(GoogleRequest req, CancellationToken ct) =>
            await LoginResponseAsync(await auth.LoginWithGoogleAsync(req.Credential, ct, req.Remember), ct);

        // Đăng nhập lại khi trình duyệt (hoặc tab) vẫn còn phiên cũ (vd đổi sang tài khoản khác): đăng nhập THÀNH CÔNG
        // thì thu hồi refresh token cũ trong cookie, tránh để phiên cũ còn hiệu lực trong DB tới hết hạn. Đăng nhập thất
        // bại thì giữ nguyên phiên cũ — người dùng vẫn đăng nhập như trước.
        private async Task<IActionResult> LoginResponseAsync(AuthResult r, CancellationToken ct)
        {
            if (r.IsSuccess) await auth.LogoutAsync(Request.Cookies[RefreshCookieName()], ct);
            return ToResponse(r, includeUser: true);
        }

        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh(CancellationToken ct)
        {
            var raw = Request.Cookies[RefreshCookieName()];
            if (string.IsNullOrEmpty(raw))
                return Unauthorized(new { message = "Không có refresh token" });

            return ToResponse(await auth.RefreshAsync(raw, ct), includeUser: false);
        }

        [HttpPost("logout")]
        public async Task<IActionResult> Logout(CancellationToken ct)
        {
            var name = RefreshCookieName();
            await auth.LogoutAsync(Request.Cookies[name], ct);
            Response.Cookies.Delete(name, new CookieOptions { Path = "/auth" });
            return Ok(new { message = "Đã đăng xuất" });
        }

        // ---------- helpers ----------
        private IActionResult ToResponse(AuthResult r, bool includeUser)
        {
            if (!r.IsSuccess) return MapError(r);

            var t = r.Tokens!;
            SetRefreshCookie(t.RefreshToken, t.RefreshExpiresAt, t.Persistent);
            return includeUser
                ? Ok(new { accessToken = t.AccessToken, user = t.User })
                : Ok(new { accessToken = t.AccessToken });
        }

        private IActionResult MapError(AuthResult r) => r.Error switch
        {
            AuthError.InvalidCredentials => Unauthorized(new { message = "Email hoặc mật khẩu không đúng" }),
            AuthError.InvalidGoogleToken => Unauthorized(new { message = "Google token không hợp lệ" }),
            AuthError.GoogleEmailNotVerified => Unauthorized(new { message = "Email Google chưa được xác minh" }),
            AuthError.InvalidRefreshToken => Unauthorized(new { message = "Refresh token không hợp lệ hoặc đã hết hạn" }),
            AuthError.AccountNotFound => Unauthorized(new { message = "Tài khoản không tồn tại" }),
            AuthError.AccountBanned => StatusCode(StatusCodes.Status403Forbidden, new
            {
                code = "ACCOUNT_BANNED",
                message = "Tài khoản của bạn đã bị khóa",
                reason = r.BanReason,
            }),
            AuthError.EmailNotVerified => StatusCode(StatusCodes.Status403Forbidden, new
            {
                code = "EMAIL_NOT_VERIFIED",
                message = "Tài khoản chưa xác minh email. Vui lòng kiểm tra hộp thư.",
            }),
            _ => StatusCode(StatusCodes.Status500InternalServerError, new { message = "Lỗi không xác định" }),
        };

        private void SetRefreshCookie(string raw, DateTime expires, bool persistent)
        {
            var opts = new CookieOptions
            {
                HttpOnly = true,
                Secure = !env.IsDevelopment(),
                SameSite = SameSiteMode.Lax,
                Path = "/auth",
            };
            if (persistent) opts.Expires = expires;   // không đặt Expires => cookie phiên (mất khi đóng trình duyệt)
            Response.Cookies.Append(RefreshCookieName(), raw, opts);
        }
    }
}
