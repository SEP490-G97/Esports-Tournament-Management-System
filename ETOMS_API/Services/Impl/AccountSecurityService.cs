using ETOMS_API.Models;
using ETOMS_API.Repositories;
using ETOMS_API.Services.Interface;
using ETOMS_API.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Net;

namespace ETOMS_API.Services.Impl
{
    public class AccountSecurityService : IAccountSecurityService
    {
        private readonly EtomsDbContext _db;
        private readonly IPasswordService _passwords;
        private readonly IOneTimeTokenService _oneTime;
        private readonly IRefreshTokenService _refresh;
        private readonly IEmailSender _email;
        private readonly AuthLinkOptions _links;
        private readonly EmailOptions _emailOptions;
        private readonly ILogger<AccountSecurityService> _log;


        public AccountSecurityService(
        EtomsDbContext db,
        IPasswordService passwords,
        IOneTimeTokenService oneTime,
        IRefreshTokenService refresh,
        IEmailSender email,
        IOptions<AuthLinkOptions> links,
        IOptions<EmailOptions> emailOptions,
        ILogger<AccountSecurityService> log)
        {
            _db = db;
            _passwords = passwords;
            _oneTime = oneTime;
            _refresh = refresh;
            _email = email;
            _links = links.Value;
            _emailOptions = emailOptions.Value;
            _log = log;
        }


        // ---------- AUTH-01 ----------
        public async Task<SecurityResult> RegisterAsync(string email, string password, string fullName, DateOnly dateOfBirth, CancellationToken ct = default)
        {
            email = (email ?? "").Trim();
            fullName = (fullName ?? "").Trim();

            if (!PasswordPolicy.IsValidEmail(email)) return SecurityResult.Fail(SecurityError.InvalidEmail);
            if (fullName.Length is < 2 or > 100) return SecurityResult.Fail(SecurityError.InvalidName);
            if (!PasswordPolicy.IsStrong(password)) return SecurityResult.Fail(SecurityError.WeakPassword);

            // Bắt buộc đủ 18 tuổi mới được tạo tài khoản.
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var age = today.Year - dateOfBirth.Year;
            if (dateOfBirth.AddYears(age) > today) age--;
            if (age < 18) return SecurityResult.Fail(SecurityError.UnderAge);

            // Collation _ci nên so sánh trực tiếp là không phân biệt hoa thường.
            if (await _db.Accounts.AnyAsync(a => a.Email == email, ct))
                return SecurityResult.Fail(SecurityError.EmailTaken);

            // Máy chưa cấu hình SMTP thì không gửi được mail xác minh -> kích hoạt ngay, nếu không
            // tài khoản mới sẽ kẹt PENDING_VERIFY mãi, không bao giờ đăng nhập được.
            var activateNow = !_emailOptions.CanSend;
            var account = new Account
            {
                Email = email,
                PasswordHash = _passwords.Hash(password),
                FullName = fullName,
                DateOfBirth = dateOfBirth,
                Role = AccountRoles.Player,
                Status = activateNow ? AccountStatuses.Active : AccountStatuses.PendingVerify,
                EmailVerifiedAt = activateNow ? DateTime.UtcNow : null
            };
            _db.Accounts.Add(account);

            try { await _db.SaveChangesAsync(ct); }
            catch (DbUpdateException)
            {
                // Hai request cùng email chạy đồng thời: UNIQUE chặn request thứ hai.
                return SecurityResult.Fail(SecurityError.EmailTaken);
            }

            if (activateNow) return SecurityResult.SuccessActivated();

            await SendVerificationMailAsync(account, ct);
            return SecurityResult.Success();
        }

        // ---------- AUTH-04 ----------
        public async Task<SecurityResult> VerifyEmailAsync(string token, CancellationToken ct = default)
        {
            var accountId = await _oneTime.ConsumeAsync(token, TokenTypes.EmailVerify, ct);
            if (accountId is null) return SecurityResult.Fail(SecurityError.InvalidToken);

            var account = await _db.Accounts.FirstOrDefaultAsync(a => a.AccountId == accountId, ct);
            if (account is null) return SecurityResult.Fail(SecurityError.InvalidToken);

            account.EmailVerifiedAt ??= DateTime.UtcNow;
            // Tài khoản BANNED giữ nguyên trạng thái, chỉ PENDING_VERIFY mới được nâng lên ACTIVE.
            if (account.Status == AccountStatuses.PendingVerify)
                account.Status = AccountStatuses.Active;

            await _db.SaveChangesAsync(ct);
            return SecurityResult.Success();
        }

        public async Task ResendVerificationAsync(string email, CancellationToken ct = default)
        {
            email = (email ?? "").Trim();
            var account = await _db.Accounts.FirstOrDefaultAsync(a => a.Email == email, ct);
            // Không tiết lộ email có tồn tại hay không: luôn im lặng nếu không đủ điều kiện.
            if (account is null || account.Status != AccountStatuses.PendingVerify) return;
            await SendVerificationMailAsync(account, ct);
        }

        // ---------- AUTH-03 ----------
        public async Task ForgotPasswordAsync(string email, CancellationToken ct = default)
        {
            email = (email ?? "").Trim();
            var account = await _db.Accounts.FirstOrDefaultAsync(a => a.Email == email, ct);
            if (account is null || account.Status == AccountStatuses.Banned) return;

            var raw = await _oneTime.IssueAsync(account.AccountId, TokenTypes.PasswordReset,
                TimeSpan.FromMinutes(_links.ResetMinutes), ct);
            var link = BuildLink("/auth/reset-password.html", raw);

            await SafeSendAsync(account.Email, "ETOMS: Đặt lại mật khẩu",
                Template($"Xin chào {WebUtility.HtmlEncode(account.FullName)},",
                    $"Bạn vừa yêu cầu đặt lại mật khẩu. Liên kết có hiệu lực trong {_links.ResetMinutes} phút.",
                    "Đặt lại mật khẩu", link,
                    "Nếu không phải bạn yêu cầu, hãy bỏ qua email này, mật khẩu hiện tại vẫn giữ nguyên."), ct);
        }

        public async Task<SecurityResult> ResetPasswordAsync(string token, string newPassword, CancellationToken ct = default)
        {
            // Kiểm tra mật khẩu TRƯỚC khi tiêu thụ token để không làm cháy link khi người dùng gõ mật khẩu yếu.
            if (!PasswordPolicy.IsStrong(newPassword)) return SecurityResult.Fail(SecurityError.WeakPassword);

            var accountId = await _oneTime.ConsumeAsync(token, TokenTypes.PasswordReset, ct);
            if (accountId is null) return SecurityResult.Fail(SecurityError.InvalidToken);

            var account = await _db.Accounts.FirstOrDefaultAsync(a => a.AccountId == accountId, ct);
            if (account is null) return SecurityResult.Fail(SecurityError.InvalidToken);
            if (account.Status == AccountStatuses.Banned) return SecurityResult.Fail(SecurityError.AccountBanned);

            account.PasswordHash = _passwords.Hash(newPassword);
            // Nhận được mail reset nghĩa là người dùng sở hữu email này.
            account.EmailVerifiedAt ??= DateTime.UtcNow;
            if (account.Status == AccountStatuses.PendingVerify)
                account.Status = AccountStatuses.Active;

            await _db.SaveChangesAsync(ct);
            await _refresh.RevokeAllAsync(account.AccountId);

            await SafeSendAsync(account.Email, "ETOMS: Mật khẩu đã được thay đổi",
                Template($"Xin chào {WebUtility.HtmlEncode(account.FullName)},",
                    "Mật khẩu tài khoản của bạn vừa được đặt lại. Mọi phiên đăng nhập cũ đã bị đăng xuất.",
                    null, null,
                    "Nếu không phải bạn thực hiện, hãy liên hệ quản trị viên ngay."), ct);

            return SecurityResult.Success();
        }

        // ---------- AUTH-05 ----------
        public async Task<SecurityResult> ChangePasswordAsync(int accountId, string currentPassword, string newPassword, CancellationToken ct = default)
        {
            var account = await _db.Accounts.FirstOrDefaultAsync(a => a.AccountId == accountId, ct);
            if (account is null) return SecurityResult.Fail(SecurityError.AccountNotFound);
            if (account.Status == AccountStatuses.Banned) return SecurityResult.Fail(SecurityError.AccountBanned);

            // Tài khoản chỉ có Google chưa có mật khẩu: phải dùng "Quên mật khẩu" để tạo lần đầu.
            if (string.IsNullOrEmpty(account.PasswordHash)) return SecurityResult.Fail(SecurityError.NoPassword);

            if (!_passwords.Verify(currentPassword ?? "", account.PasswordHash))
                return SecurityResult.Fail(SecurityError.WrongPassword);
            if (!PasswordPolicy.IsStrong(newPassword)) return SecurityResult.Fail(SecurityError.WeakPassword);
            if (currentPassword == newPassword) return SecurityResult.Fail(SecurityError.SamePassword);

            account.PasswordHash = _passwords.Hash(newPassword);
            await _db.SaveChangesAsync(ct);

            // Thu hồi toàn bộ refresh token, kể cả thiết bị hiện tại (frontend sẽ đưa về trang đăng nhập).
            await _refresh.RevokeAllAsync(account.AccountId);
            return SecurityResult.Success();
        }

        // ---------- helpers ----------
        private async Task SendVerificationMailAsync(Account account, CancellationToken ct)
        {
            var raw = await _oneTime.IssueAsync(account.AccountId, TokenTypes.EmailVerify,
                TimeSpan.FromHours(_links.VerifyHours), ct);
            var link = BuildLink("/auth/verify-email.html", raw);

            await SafeSendAsync(account.Email, "ETOMS: Xác minh địa chỉ email",
                Template($"Xin chào {WebUtility.HtmlEncode(account.FullName)},",
                    $"Cảm ơn bạn đã đăng ký ETOMS. Vui lòng xác minh email, liên kết có hiệu lực trong {_links.VerifyHours} giờ.",
                    "Xác minh email", link,
                    "Nếu bạn không tạo tài khoản này, hãy bỏ qua email."), ct);
        }

        private string BuildLink(string path, string rawToken) =>
            $"{_links.BaseUrl.TrimEnd('/')}{path}?token={Uri.EscapeDataString(rawToken)}";

        /// <summary>Lỗi gửi mail không được làm hỏng request (tài khoản đã tạo, người dùng có thể bấm gửi lại).</summary>
        private async Task SafeSendAsync(string to, string subject, string html, CancellationToken ct)
        {
            try { await _email.SendAsync(to, subject, html, ct); }
            catch (Exception ex) { _log.LogError(ex, "Gửi mail '{Subject}' tới {To} thất bại", subject, to); }
        }

        private static string Template(string greeting, string body, string? buttonText, string? link, string footer)
        {
            var button = buttonText is null || link is null ? "" :
                $"<p><a href=\"{WebUtility.HtmlEncode(link)}\" style=\"display:inline-block;padding:10px 20px;background:#2563eb;color:#fff;text-decoration:none;border-radius:6px\">{buttonText}</a></p>" +
                $"<p style=\"font-size:12px;color:#666\">Nếu nút không bấm được, copy liên kết này vào trình duyệt:<br>{WebUtility.HtmlEncode(link)}</p>";
            return $"<div style=\"font-family:Arial,sans-serif;max-width:520px\"><p>{greeting}</p><p>{body}</p>{button}<p style=\"font-size:12px;color:#666\">{footer}</p></div>";
        }
    }
}
