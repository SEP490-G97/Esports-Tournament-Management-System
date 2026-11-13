using ETOMS_API.Models;
using ETOMS_API.Repositories;
using ETOMS_API.Services.Interface;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace ETOMS_API.Services.Impl
{
    public class OneTimeTokenService : IOneTimeTokenService
    {
        private readonly EtomsDbContext _db;
        private readonly ITokenService _tokens;

        public OneTimeTokenService(EtomsDbContext db, ITokenService tokens)
        {
            _db = db;
            _tokens = tokens;
        }

        public async Task<string> IssueAsync(int accountId, string tokenType, TimeSpan lifetime, CancellationToken ct = default)
        {
            var now = DateTime.UtcNow;

            // Chỉ link mới nhất có hiệu lực: link cũ của cùng loại bị vô hiệu hóa.
            await _db.AuthTokens
                .Where(t => t.AccountId == accountId && t.TokenType == tokenType && t.UsedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.UsedAt, now), ct);

            var raw = Base64Url(RandomNumberGenerator.GetBytes(32));
            _db.AuthTokens.Add(new AuthToken
            {
                AccountId= accountId,
                TokenHash = _tokens.HashToken(raw),
                TokenType = tokenType,
                ExpiresAt = now.Add(lifetime),
                CreatedAt = now
            });
            await _db.SaveChangesAsync(ct);
            return raw;
        }

        public async Task<int?> ConsumeAsync(string rawToken, string tokenType, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(rawToken) || rawToken.Length > 200) return null;

            var hash = _tokens.HashToken(rawToken);
            var now = DateTime.UtcNow;

            var affected = await _db.AuthTokens
                .Where(t => t.TokenHash == hash && t.TokenType == tokenType && t.UsedAt == null && t.ExpiresAt > now)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.UsedAt, now), ct);

            if (affected == 0) return null;

            return await _db.AuthTokens
                .Where(t => t.TokenHash == hash)
                .Select(t => (int?)t.AccountId)
                .FirstOrDefaultAsync(ct);
        }

        private static string Base64Url(byte[] bytes) =>
            Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
