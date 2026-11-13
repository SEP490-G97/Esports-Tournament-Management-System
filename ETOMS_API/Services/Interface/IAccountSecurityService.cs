using ETOMS_API.Utils;

namespace ETOMS_API.Services.Interface
{
    public interface IAccountSecurityService
    {
        Task<SecurityResult> RegisterAsync(string email, string password, string fullName, CancellationToken ct = default);
        Task<SecurityResult> VerifyEmailAsync(string token, CancellationToken ct = default);
        Task ResendVerificationAsync(string email, CancellationToken ct = default);
        Task ForgotPasswordAsync(string email, CancellationToken ct = default);
        Task<SecurityResult> ResetPasswordAsync(string token, string newPassword, CancellationToken ct = default);
        Task<SecurityResult> ChangePasswordAsync(int accountId, string currentPassword, string newPassword, CancellationToken ct = default);
    }
}
