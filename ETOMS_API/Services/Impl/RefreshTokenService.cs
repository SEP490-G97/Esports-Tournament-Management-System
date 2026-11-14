using ETOMS_API.DTOs.Auth;
using ETOMS_API.Models;
using ETOMS_API.Repositories;
using ETOMS_API.Services.Interface;
using ETOMS_API.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using static ETOMS_API.DTOs.Auth.AuthDtos;

namespace ETOMS_API.Services.Impl
{
    public class RefreshTokenService(EtomsDbContext db, ITokenService tokens, IOptions<JwtOptions> options) : IRefreshTokenService {
        private readonly JwtOptions _o = options.Value;


        public async Task<ConsumeResult> ConsumeAsync(string rawToken, CancellationToken ct = default)
        {
            var hash = tokens.HashToken(rawToken);
            var rt = await db.AuthTokens.AsNoTracking()
                .FirstOrDefaultAsync(t => t.TokenHash == hash && t.TokenType == TokenTypes.Refresh, ct);

            if (rt is null || rt.ExpiresAt <= DateTime.UtcNow)
                return ConsumeResult.Fail();

            // Token đã dùng mà bị dùng lại => thu hồi token của account.
            // Ngoại lệ: dùng lại trong 60 giây đầu thường là 2 tab/2 request refresh cùng lúc của chính
            // người dùng (web nhiều trang, không phải SPA) — chỉ từ chối lượt này, không đăng xuất cả phiên.
            if (rt.UsedAt is not null)
            {
                if (DateTime.UtcNow - rt.UsedAt.Value > TimeSpan.FromSeconds(60))
                    await RevokeAllAsync(rt.AccountId, ct);
                return ConsumeResult.Fail();
            }

            // Atomic: nếu 2 request đến cùng lúc, chỉ 1 request ok, request còn lại sẽ bị fail vì UsedAt đã được set
            var claimed = await db.AuthTokens
                .Where(t => t.AuthTokenId == rt.AuthTokenId && t.UsedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.UsedAt, DateTime.UtcNow), ct);
            var persistent = (rt.ExpiresAt - rt.CreatedAt) > TimeSpan.FromHours(_o.SessionRefreshHours) + TimeSpan.FromMinutes(1);

            return claimed == 1 ? new ConsumeResult(true, rt.AccountId, persistent) : ConsumeResult.Fail();
        }

        public async Task<(string Raw, DateTime ExpiresAt)> CreateAsync(int accountId, bool persistent, CancellationToken ct = default)
        {
            var raw = tokens.GenerateRefreshToken();
            var now = DateTime.UtcNow;
            var expires = now + (persistent
                ? TimeSpan.FromDays(_o.RefreshDays)
                : TimeSpan.FromHours(_o.SessionRefreshHours));

            db.AuthTokens.Add(new AuthToken
            {
                AccountId = accountId,
                TokenHash = tokens.HashToken(raw),
                TokenType = TokenTypes.Refresh,
                ExpiresAt = expires,
                CreatedAt = now,
            });
            await db.SaveChangesAsync(ct);
            return (raw, expires);
        }

        public async Task RevokeAsync(string rawToken, CancellationToken ct = default)
        {
            var hash = tokens.HashToken(rawToken);
            await db.AuthTokens
                .Where(t => t.TokenHash == hash && t.TokenType == TokenTypes.Refresh && t.UsedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.UsedAt, DateTime.UtcNow), ct);
        }

        public async Task RevokeAllAsync(int accountId, CancellationToken ct = default)
        {
            await db.AuthTokens
                .Where(t => t.AccountId == accountId && t.TokenType == TokenTypes.Refresh && t.UsedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.UsedAt, DateTime.UtcNow), ct);
        }
    }
}
