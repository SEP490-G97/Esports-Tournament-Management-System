using static ETOMS_API.DTOs.Auth.AuthDtos;

namespace ETOMS_API.Services.Interface
{
    public interface IAuthService
    {
        Task<AuthResult> LoginAsync(string email, string password, bool remember, CancellationToken ct = default);
        Task<AuthResult> LoginWithGoogleAsync(string credential, CancellationToken ct = default);
        Task<AuthResult> RefreshAsync(string refreshToken, CancellationToken ct = default);
        Task LogoutAsync(string? refreshToken, CancellationToken ct = default);
    }
}
