using ETOMS_API.Services.Interface;
using ETOMS_API.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ETOMS_API.Controllers
{

    public record RegisterRequest(string Email, string Password, string FullName);
    public record EmailRequest(string Email);
    public record TokenRequest(string Token);
    public record ResetPasswordRequest(string Token, string NewPassword);
    public record ChangePasswordRequest(string CurrentPassword, string NewPassword);
    [ApiController]
    public class AccountSecurityController : ControllerBase
    {
        private readonly IAccountSecurityService _svc;
        public AccountSecurityController(IAccountSecurityService svc) => _svc = svc;

        // AUTH-01
        [HttpPost("/auth/register")]
        [EnableRateLimiting("mail")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest req, CancellationToken ct)
        {
            var r = await _svc.RegisterAsync(req.Email, req.Password, req.FullName, ct);
            return r.Ok
                ? StatusCode(201, new { message = "Đăng ký thành công. Vui lòng kiểm tra email để kích hoạt tài khoản." })
                : Map(r.Error);
        }

        // AUTH-04
        [HttpPost("/auth/verify-email")]
        [EnableRateLimiting("login")]
        public async Task<IActionResult> VerifyEmail([FromBody] TokenRequest req, CancellationToken ct)
        {
            var r = await _svc.VerifyEmailAsync(req.Token, ct);
            return r.Ok ? Ok(new { message = "Xác minh email thành công." }) : Map(r.Error);
        }

        [HttpPost("/auth/resend-verification")]
        [EnableRateLimiting("mail")]
        public async Task<IActionResult> ResendVerification([FromBody] EmailRequest req, CancellationToken ct)
        {
            await _svc.ResendVerificationAsync(req.Email, ct);
            return Ok(new { message = "Nếu email này đang chờ xác minh, một liên kết mới đã được gửi." });
        }

        // AUTH-03
        [HttpPost("/auth/forgot-password")]
        [EnableRateLimiting("mail")]
        public async Task<IActionResult> ForgotPassword([FromBody] EmailRequest req, CancellationToken ct)
        {
            await _svc.ForgotPasswordAsync(req.Email, ct);
            // Luôn trả cùng một thông báo, không lộ email có tồn tại hay không.
            return Ok(new { message = "Nếu email tồn tại trong hệ thống, liên kết đặt lại mật khẩu đã được gửi." });
        }

        [HttpPost("/auth/reset-password")]
        [EnableRateLimiting("login")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest req, CancellationToken ct)
        {
            var r = await _svc.ResetPasswordAsync(req.Token, req.NewPassword, ct);
            return r.Ok ? Ok(new { message = "Đặt lại mật khẩu thành công. Vui lòng đăng nhập." }) : Map(r.Error);
        }

        // AUTH-05
        [HttpPost("/api/me/change-password")]
        [Authorize]
        [EnableRateLimiting("login")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest req, CancellationToken ct)
        {
            // MapInboundClaims = false nên claim vẫn tên "sub".
            if (!int.TryParse(User.FindFirst("sub")?.Value, out var accountId)) return Unauthorized();

            var r = await _svc.ChangePasswordAsync(accountId, req.CurrentPassword, req.NewPassword, ct);
            return r.Ok
                ? Ok(new { message = "Đổi mật khẩu thành công. Vui lòng đăng nhập lại." })
                : Map(r.Error);
        }

        private IActionResult Map(SecurityError e) => e switch
        {
            SecurityError.InvalidEmail => BadRequest(new { code = "INVALID_EMAIL", message = "Email không hợp lệ." }),
            SecurityError.InvalidName => BadRequest(new { code = "INVALID_NAME", message = "Họ tên phải từ 2 đến 100 ký tự." }),
            SecurityError.WeakPassword => BadRequest(new { code = "WEAK_PASSWORD", message = PasswordPolicy.Message }),
            SecurityError.EmailTaken => Conflict(new { code = "EMAIL_TAKEN", message = "Email này đã được đăng ký." }),
            SecurityError.InvalidToken => BadRequest(new { code = "INVALID_TOKEN", message = "Liên kết không hợp lệ, đã hết hạn hoặc đã được sử dụng." }),
            SecurityError.WrongPassword => BadRequest(new { code = "WRONG_PASSWORD", message = "Mật khẩu hiện tại không đúng." }),
            SecurityError.SamePassword => BadRequest(new { code = "SAME_PASSWORD", message = "Mật khẩu mới phải khác mật khẩu hiện tại." }),
            SecurityError.NoPassword => BadRequest(new { code = "NO_PASSWORD", message = "Tài khoản này chưa có mật khẩu (đăng nhập bằng Google). Hãy dùng chức năng Quên mật khẩu để tạo mật khẩu." }),
            SecurityError.AccountBanned => StatusCode(403, new { code = "ACCOUNT_BANNED", message = "Tài khoản đã bị khóa." }),
            _ => Unauthorized()
        };
    }
}
