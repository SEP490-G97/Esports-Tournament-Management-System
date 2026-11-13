using ETOMS_API.Services.Interface;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Org.BouncyCastle.Asn1.Ocsp;
using static ETOMS_API.DTOs.Auth.AuthDtos;
using static Google.Apis.Requests.BatchRequest;

namespace ETOMS_API.Controllers
{
    [ApiController, Route("auth")]
    public class AuthController(IAuthService auth, IWebHostEnvironment env) : ControllerBase
    {
        private const string CookieName = "refresh_token";

        [HttpPost("login"), EnableRateLimiting("login")]
        public async Task<IActionResult> Login(LoginRequest req, CancellationToken ct) =>
            ToResponse(await auth.LoginAsync(req.Email, req.Password, req.Remember, ct), includeUser: true);

        // AUTH-01: STUB TẠM thay Vũ Đức Hải — xem ghi chú ở IAuthService.RegisterAsync.
        [HttpPost("register")]
        public async Task<IActionResult> Register(RegisterRequest req, CancellationToken ct)
        {
            var result = await auth.RegisterAsync(req, ct);
            return result.IsSuccess
                ? Ok(new { message = result.Message })
                : StatusCode(result.StatusCode, new { message = result.Message, fieldErrors = result.FieldErrors });
        }

        [HttpPost("google")]
        public async Task<IActionResult> Google(GoogleRequest req, CancellationToken ct) =>
            ToResponse(await auth.LoginWithGoogleAsync(req.Credential, ct), includeUser: true);

        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh(CancellationToken ct)
        {
            var raw = Request.Cookies[CookieName];
            if (string.IsNullOrEmpty(raw))
                return Unauthorized(new { message = "Không có refresh token" });

            return ToResponse(await auth.RefreshAsync(raw, ct), includeUser: false);
        }

        [HttpPost("logout")]
        public async Task<IActionResult> Logout(CancellationToken ct)
        {
            await auth.LogoutAsync(Request.Cookies[CookieName], ct);
            Response.Cookies.Delete(CookieName, new CookieOptions { Path = "/auth" });
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
            Response.Cookies.Append(CookieName, raw, opts);
        }
    }
}
