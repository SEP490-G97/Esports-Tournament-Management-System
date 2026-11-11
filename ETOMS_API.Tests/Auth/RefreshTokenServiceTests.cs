using ETOMS_API.Controllers;
using ETOMS_API.Models;
using ETOMS_API.Services.Impl;
using ETOMS_API.Services.Interface;
using ETOMS_API.Utils;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;
using ETOMS_API.Repositories;

namespace ETOMS_API.Tests.Auth
{
    public class RefreshTokenServiceTests : IDisposable
    {
        private readonly SqliteConnection _conn;
        private readonly EtomsDbContext _db;
        private readonly TokenService _tokens;
        private readonly RefreshTokenService _sut;
        private readonly JwtOptions _opt = new()
        {
            Key = "test-key-test-key-test-key-test-key-1234",
            Issuer = "test",
            Audience = "test",
            AccessMinutes = 15,
            RefreshDays = 7,
            SessionRefreshHours = 12,
        };

        public RefreshTokenServiceTests()
        {
            _conn = new SqliteConnection("DataSource=:memory:");
            _conn.Open();
            _db = new EtomsDbContext(new DbContextOptionsBuilder<EtomsDbContext>().UseSqlite(_conn).Options);
            _db.Database.EnsureCreated();

            _tokens = new TokenService(Options.Create(_opt));
            _sut = new RefreshTokenService(_db, _tokens, Options.Create(_opt));
        }

        public void Dispose() { _db.Dispose(); _conn.Dispose(); }

        private AuthToken Row(string raw, string type = TokenTypes.Refresh, int accountId = 1,
                              TimeSpan? lifetime = null, DateTime? usedAt = null)
        {
            var now = DateTime.UtcNow;
            var row = new AuthToken
            {
                AccountId = accountId,
                TokenHash = _tokens.HashToken(raw),
                TokenType = type,
                CreatedAt = now,
                ExpiresAt = now + (lifetime ?? TimeSpan.FromDays(7)),
                UsedAt = usedAt,
            };
            _db.AuthTokens.Add(row);
            _db.SaveChanges();
            return row;
        }

        // ----------- Create -----------

        [Fact]
        public async Task Create_Persistent_LivesRefreshDays_AndStoresHashNotRaw()
        {
            var (raw, expires) = await _sut.CreateAsync(1, persistent: true);

            var row = await _db.AuthTokens.SingleAsync();
            Assert.Equal(_tokens.HashToken(raw), row.TokenHash);
            Assert.NotEqual(raw, row.TokenHash);
            Assert.Equal(TokenTypes.Refresh, row.TokenType);
            Assert.InRange((expires - DateTime.UtcNow).TotalDays, 6.99, 7.01);
        }

        [Fact]
        public async Task Create_NotPersistent_LivesSessionHours()
        {
            var (_, expires) = await _sut.CreateAsync(1, persistent: false);

            Assert.InRange((expires - DateTime.UtcNow).TotalHours, 11.9, 12.1);
        }

        // ----------- Consume -----------

        [Fact]
        public async Task Consume_ValidPersistentToken_Succeeds_AndMarksUsed()
        {
            Row("tok", lifetime: TimeSpan.FromDays(7));

            var r = await _sut.ConsumeAsync("tok");

            Assert.True(r.Success);
            Assert.Equal(1, r.AccountId);
            Assert.True(r.Persistent);
            Assert.NotNull((await _db.AuthTokens.AsNoTracking().SingleAsync()).UsedAt);
        }

        [Fact]
        public async Task Consume_SessionToken_ReportsNotPersistent()
        {
            Row("tok", lifetime: TimeSpan.FromHours(12));

            var r = await _sut.ConsumeAsync("tok");

            Assert.True(r.Success);
            Assert.False(r.Persistent);
        }

        [Fact]
        public async Task Consume_UnknownToken_Fails()
        {
            Assert.False((await _sut.ConsumeAsync("nope")).Success);
        }

        [Fact]
        public async Task Consume_ExpiredToken_Fails()
        {
            Row("tok", lifetime: TimeSpan.FromMinutes(-5));

            Assert.False((await _sut.ConsumeAsync("tok")).Success);
        }

        [Fact]
        public async Task Consume_OtherTokenType_IsNotAcceptedAsRefreshToken()
        {
            Row("tok", type: "PASSWORD_RESET");

            Assert.False((await _sut.ConsumeAsync("tok")).Success);
        }

        [Fact]
        public async Task Consume_ReusedToken_Fails_AndRevokesAllOfThatAccount()
        {
            Row("old", usedAt: DateTime.UtcNow.AddMinutes(-1));   // token đã dùng, bị đem dùng lại
            Row("other-device");                                    // phiên hợp lệ khác của cùng tài khoản

            var r = await _sut.ConsumeAsync("old");

            Assert.False(r.Success);
            var other = await _db.AuthTokens.AsNoTracking()
                .SingleAsync(t => t.TokenHash == _tokens.HashToken("other-device"));
            Assert.NotNull(other.UsedAt);                           // bị thu hồi
        }

        [Fact]
        public async Task Consume_SameTokenTwice_SecondCallFails()
        {
            Row("tok");

            Assert.True((await _sut.ConsumeAsync("tok")).Success);
            Assert.False((await _sut.ConsumeAsync("tok")).Success);
        }

        // ----------- Revoke -----------

        [Fact]
        public async Task RevokeAll_OnlyAffectsRefreshTokensOfThatAccount()
        {
            Row("a1", accountId: 1);
            Row("a2", accountId: 1);
            Row("b1", accountId: 2);
            Row("verify", type: "EMAIL_VERIFY", accountId: 1);

            await _sut.RevokeAllAsync(1);

            bool Used(string raw) => _db.AuthTokens.AsNoTracking()
                .Single(t => t.TokenHash == _tokens.HashToken(raw)).UsedAt != null;
            Assert.True(Used("a1"));
            Assert.True(Used("a2"));
            Assert.False(Used("b1"));        // tài khoản khác
            Assert.False(Used("verify"));    // loại token khác
        }

        [Fact]
        public async Task Revoke_SingleToken_MarksOnlyThatOne()
        {
            Row("t1");
            Row("t2");

            await _sut.RevokeAsync("t1");

            bool Used(string raw) => _db.AuthTokens.AsNoTracking()
                .Single(t => t.TokenHash == _tokens.HashToken(raw)).UsedAt != null;
            Assert.True(Used("t1"));
            Assert.False(Used("t2"));
        }
    }
}
