using static ETOMS_API.DTOs.Auth.AuthDtos;

namespace ETOMS_API.Services.Interface
{
    public interface IRefreshTokenService
    {
        Task<(string Raw, DateTime ExpiresAt)> CreateAsync(int accountId, bool persistent, CancellationToken ct = default);

        /// <summary>Kiểm tra + đánh dấu đã dùng (rotation). Token bị dùng lại sẽ thu hồi toàn bộ.</summary>
        Task<ConsumeResult> ConsumeAsync(string rawToken, CancellationToken ct = default);

        Task RevokeAsync(string rawToken, CancellationToken ct = default);
        Task RevokeAllAsync(int accountId, CancellationToken ct = default);
    }
}
