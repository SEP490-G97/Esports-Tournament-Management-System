using ETOMS_API.Utils;
using static ETOMS_API.DTOs.Auth.AuthDtos;

namespace ETOMS_API.Services.Interface
{
    public interface IAuthService
    {
        Task<AuthResult> LoginAsync(string email, string password, bool remember, CancellationToken ct = default);
        Task<AuthResult> LoginWithGoogleAsync(string credential, CancellationToken ct = default);
        Task<AuthResult> RefreshAsync(string refreshToken, CancellationToken ct = default);
        Task LogoutAsync(string? refreshToken, CancellationToken ct = default);

        // AUTH-01: STUB TẠM thay module AUTH (Vũ Đức Hải) — trang đăng ký trước đó nạp nhầm
        // script của dev-auth nên không gửi được gì cả. Chưa gửi email xác minh thật (cần SMTP
        // thật của Hải), nên tài khoản mới được kích hoạt thẳng (Status=ACTIVE) để demo đăng
        // nhập được ngay; không phải bài làm cá nhân của Hải.
        Task<Result> RegisterAsync(RegisterRequest req, CancellationToken ct = default);
    }
}
