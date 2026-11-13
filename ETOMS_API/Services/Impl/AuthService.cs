using ETOMS_API.DTOs.Auth;
using ETOMS_API.Models;
using ETOMS_API.Repositories.Interface;
using ETOMS_API.Services.Interface;
using ETOMS_API.Utils;
using static ETOMS_API.DTOs.Auth.AuthDtos;

namespace ETOMS_API.Services.Impl
{
    public class AuthService(
    IAccountRepository accounts,
    IPasswordService passwords,
    ITokenService tokens,
    IRefreshTokenService refreshTokens,
    IGoogleTokenValidator google) : IAuthService
    {
        // ----------Login with Email + Password ----------

        public async Task<AuthResult> LoginAsync(string email, string password, bool remember, CancellationToken ct = default)
        {
            var acc = await accounts.FindByEmailAsync(email, ct);
            var ok = acc?.PasswordHash is not null && passwords.Verify(password, acc.PasswordHash);
            if (!ok) return AuthResult.Fail(AuthError.InvalidCredentials);

            return await IssueAsync(acc!, remember, ct);
        }

        // ---------- AUTH-01: Đăng ký (STUB TẠM thay Vũ Đức Hải — xem ghi chú ở interface) ----------
        public async Task<Result> RegisterAsync(RegisterRequest req, CancellationToken ct = default)
        {
            var email = req.Email.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(email))
                return Result.FieldFailure(nameof(req.Email), "Vui lòng nhập email.");
            if (string.IsNullOrWhiteSpace(req.FullName))
                return Result.FieldFailure(nameof(req.FullName), "Vui lòng nhập họ tên.");
            if (string.IsNullOrWhiteSpace(req.Password) || req.Password.Length < 8)
                return Result.FieldFailure(nameof(req.Password), "Mật khẩu phải có ít nhất 8 ký tự.");

            var age = DateOnly.FromDateTime(DateTime.UtcNow).Year - req.DateOfBirth.Year;
            if (req.DateOfBirth.AddYears(age) > DateOnly.FromDateTime(DateTime.UtcNow)) age--;
            if (age < 18)
                return Result.FieldFailure(nameof(req.DateOfBirth), "Bạn phải đủ 18 tuổi trở lên mới được tạo tài khoản.");

            if (await accounts.FindByEmailAsync(email, ct) is not null)
                return Result.FieldFailure(nameof(req.Email), "Email này đã được đăng ký.");

            var acc = new Account
            {
                Email = email,
                PasswordHash = passwords.Hash(req.Password),
                FullName = req.FullName.Trim(),
                DateOfBirth = req.DateOfBirth,
                Role = "PLAYER",
                // Chưa có luồng gửi email xác minh thật (cần SMTP của module AUTH) nên kích hoạt
                // thẳng để demo đăng nhập được ngay. Hải thay bằng PENDING_VERIFY + email thật sau.
                Status = "ACTIVE",
                NotifyInApp = true,
                NotifyEmail = true,
                OrganizerRequestStatus = "NONE",
                CreatedAt = DateTime.UtcNow
            };
            await accounts.AddAsync(acc, ct);
            await accounts.SaveChangesAsync(ct);
            return Result.Success("Đăng ký thành công. Bạn có thể đăng nhập ngay.");
        }

        // ---------- Helpers ----------
        private async Task<AuthResult> IssueAsync(Account acc, bool persistent, CancellationToken ct)
        {
            if (acc.Status == AccountStatuses.Banned)
                return AuthResult.Fail(AuthError.AccountBanned, BanReasonOf(acc));
            if (acc.Status == AccountStatuses.PendingVerify)
                return AuthResult.Fail(AuthError.EmailNotVerified);

            var access = tokens.CreateAccessToken(acc);
            var (raw, expires) = await refreshTokens.CreateAsync(acc.AccountId, persistent, ct);
            var dto = new AccountDto(acc.AccountId, acc.Email, acc.FullName, acc.AvatarUrl, acc.Role);

            return AuthResult.Ok(new AuthTokens(access, raw, expires, persistent, dto));
        }

        private static string BanReasonOf(Account acc) =>
            string.IsNullOrWhiteSpace(acc.BanReason) ? "Không có lý do được ghi nhận" : acc.BanReason;

        // ---------- Google ----------
        public async Task<AuthResult> LoginWithGoogleAsync(string credential, CancellationToken ct = default)
        {
            var g = await google.ValidateAsync(credential);
            if (g is null) return AuthResult.Fail(AuthError.InvalidGoogleToken);
            if (!g.EmailVerified) return AuthResult.Fail(AuthError.GoogleEmailNotVerified);

            // Tìm theo GoogleSubjectId -> theo Email (liên kết) -> tạo mới
            var acc = await accounts.FindByGoogleSubjectAsync(g.Subject, ct)
                   ?? await accounts.FindByEmailAsync(g.Email, ct);

            if (acc is null)
            {
                acc = new Account
                {
                    Email = g.Email,
                    FullName = g.Name ?? g.Email,
                    AvatarUrl = g.Picture,
                    GoogleSubjectId = g.Subject,
                    Role = AccountRoles.Player,
                    Status = AccountStatuses.Active,   // Google đã xác minh email nên vào thẳng ACTIVE
                    OrganizerRequestStatus = OrganizerRequestStatuses.None,
                    EmailVerifiedAt = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                };
                await accounts.AddAsync(acc, ct);
            }
            else if (acc.GoogleSubjectId is null)
            {
                acc.GoogleSubjectId = g.Subject;
                acc.EmailVerifiedAt ??= DateTime.UtcNow;
                acc.AvatarUrl ??= g.Picture;
                if (acc.Status == AccountStatuses.PendingVerify)
                    acc.Status = AccountStatuses.Active;
            }
            await accounts.SaveChangesAsync(ct);

            return await IssueAsync(acc, persistent: true, ct);
        }

        // ---------- Logout ----------
        public async Task LogoutAsync(string? refreshToken, CancellationToken ct = default)
        {
            if (!string.IsNullOrEmpty(refreshToken))
                await refreshTokens.RevokeAsync(refreshToken, ct);
        }

        // ---------- Refresh ----------
        public async Task<AuthResult> RefreshAsync(string refreshToken, CancellationToken ct = default)
        {
            var consumed = await refreshTokens.ConsumeAsync(refreshToken, ct);
            if (!consumed.Success) return AuthResult.Fail(AuthError.InvalidRefreshToken);

            var acc = await accounts.FindByIdAsync(consumed.AccountId, ct);
            if (acc is null) return AuthResult.Fail(AuthError.AccountNotFound);

            if (acc.Status == AccountStatuses.Banned)   // bị ban SAU khi đã đăng nhập
            {
                await refreshTokens.RevokeAllAsync(acc.AccountId, ct);
                return AuthResult.Fail(AuthError.AccountBanned, BanReasonOf(acc));
            }

            return await IssueAsync(acc, consumed.Persistent, ct);
        }


    }
}
