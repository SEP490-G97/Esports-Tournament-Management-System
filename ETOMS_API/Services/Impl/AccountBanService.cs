using ETOMS_API.Repositories;
using ETOMS_API.Services.Interface;
using ETOMS_API.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace ETOMS_API.Services.Impl
{
    public class AccountBanService(EtomsDbContext db, IMemoryCache cache) : IAccountBanService
    {
        public async Task<bool> BanAsync(int accountId, string reason, CancellationToken ct = default)
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);

            var updated = await db.Accounts.Where(a => a.AccountId == accountId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(a => a.Status, AccountStatuses.Banned)
                    .SetProperty(a => a.BanReason, reason), ct);
            if (updated == 0) return false;

            // Đăng xuất user ra khỏi mọi thiết bị
            await db.AuthTokens
                .Where(t => t.AccountId == accountId && t.TokenType == TokenTypes.Refresh && t.UsedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.UsedAt, DateTime.UtcNow), ct);

            await tx.CommitAsync(ct);
            cache.Remove(AccountStatusCache.Key(accountId)); // chặn ngay request kế tiếp
            return true;
        }

        public async Task<bool> UnbanAsync(int accountId, CancellationToken ct = default)
        {
            var updated = await db.Accounts.Where(a => a.AccountId == accountId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(a => a.Status, AccountStatuses.Active)
                    .SetProperty(a => a.BanReason, (string?)null), ct);
            return updated > 0;
        }
    }
}
