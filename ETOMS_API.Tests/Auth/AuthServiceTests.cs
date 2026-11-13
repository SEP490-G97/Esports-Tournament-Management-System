using ETOMS_API.Models;
using ETOMS_API.Repositories.Interface;
using ETOMS_API.Services.Impl;
using ETOMS_API.Services.Interface;
using ETOMS_API.Utils;
using Moq;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit;
using static ETOMS_API.DTOs.Auth.AuthDtos;

namespace ETOMS_API.Tests.Auth
{
    public class AuthServiceTests
    {
        private readonly Mock<IAccountRepository> _accounts = new();
        private readonly Mock<IPasswordService> _passwords = new();
        private readonly Mock<ITokenService> _tokens = new();
        private readonly Mock<IRefreshTokenService> _refresh = new();
        private readonly Mock<IGoogleTokenValidator> _google = new();

        private static readonly CancellationToken Any = default;

        public AuthServiceTests()
        {
            _tokens.Setup(t => t.CreateAccessToken(It.IsAny<Account>())).Returns("access-token");
            _refresh.Setup(r => r.CreateAsync(It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(("raw-refresh", DateTime.UtcNow.AddDays(7)));
        }

        private AuthService Sut() =>
            new(_accounts.Object, _passwords.Object, _tokens.Object, _refresh.Object, _google.Object);

        private static Account MakeAccount(string status = AccountStatuses.Active, string? reason = null,
                                           string? passwordHash = "hash", string? googleSub = null) => new()
                                           {
                                               AccountId = 1,
                                               Email = "a@x.com",
                                               FullName = "A",
                                               Role = AccountRoles.Player,
                                               Status = status,
                                               BanReason = reason,
                                               PasswordHash = passwordHash,
                                               GoogleSubjectId = googleSub,
                                           };

        private void GivenPasswordLogin(Account? acc, bool passwordOk)
        {
            _accounts.Setup(r => r.FindByEmailAsync("a@x.com", It.IsAny<CancellationToken>())).ReturnsAsync(acc);
            _passwords.Setup(p => p.Verify(It.IsAny<string>(), It.IsAny<string>())).Returns(passwordOk);
        }

        private void VerifyNoTokenIssued() =>
            _refresh.Verify(r => r.CreateAsync(It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);

        // ======================= LOGIN: EMAIL + PASSWORD =======================

        [Fact]
        public async Task Login_ValidCredentials_ReturnsTokensAndUser()
        {
            GivenPasswordLogin(MakeAccount(), passwordOk: true);

            var r = await Sut().LoginAsync("a@x.com", "pw", remember: true);

            Assert.True(r.IsSuccess);
            Assert.Equal("access-token", r.Tokens!.AccessToken);
            Assert.Equal("raw-refresh", r.Tokens.RefreshToken);
            Assert.Equal("a@x.com", r.Tokens.User.Email);
            Assert.Equal(AccountRoles.Player, r.Tokens.User.Role);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task Login_RememberFlag_IsPassedToTokenCreation(bool remember)
        {
            GivenPasswordLogin(MakeAccount(), passwordOk: true);

            var r = await Sut().LoginAsync("a@x.com", "pw", remember);

            Assert.Equal(remember, r.Tokens!.Persistent);
            _refresh.Verify(x => x.CreateAsync(1, remember, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Login_WrongPassword_ReturnsInvalidCredentials_AndIssuesNothing()
        {
            GivenPasswordLogin(MakeAccount(), passwordOk: false);

            var r = await Sut().LoginAsync("a@x.com", "bad", true);

            Assert.Equal(AuthError.InvalidCredentials, r.Error);
            VerifyNoTokenIssued();
        }

        [Fact]
        public async Task Login_UnknownEmail_ReturnsInvalidCredentials_WithoutCheckingPassword()
        {
            GivenPasswordLogin(null, passwordOk: true);

            var r = await Sut().LoginAsync("a@x.com", "pw", true);

            Assert.Equal(AuthError.InvalidCredentials, r.Error);
            _passwords.Verify(p => p.Verify(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task Login_GoogleOnlyAccount_WithoutPasswordHash_ReturnsInvalidCredentials()
        {
            GivenPasswordLogin(MakeAccount(passwordHash: null, googleSub: "g1"), passwordOk: true);

            var r = await Sut().LoginAsync("a@x.com", "pw", true);

            Assert.Equal(AuthError.InvalidCredentials, r.Error);
        }

        // ======================= LOGIN: BAN / PENDING =======================

        [Fact]
        public async Task Login_BannedAccount_ReturnsBannedWithReason_AndIssuesNothing()
        {
            GivenPasswordLogin(MakeAccount(AccountStatuses.Banned, "Spam bình luận"), passwordOk: true);

            var r = await Sut().LoginAsync("a@x.com", "pw", true);

            Assert.Equal(AuthError.AccountBanned, r.Error);
            Assert.Equal("Spam bình luận", r.BanReason);
            VerifyNoTokenIssued();
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task Login_BannedWithoutReason_UsesDefaultReason(string? reason)
        {
            GivenPasswordLogin(MakeAccount(AccountStatuses.Banned, reason), passwordOk: true);

            var r = await Sut().LoginAsync("a@x.com", "pw", true);

            Assert.Equal(AuthError.AccountBanned, r.Error);
            Assert.Equal("Không có lý do được ghi nhận", r.BanReason);
        }

        [Fact]
        public async Task Login_BannedAccount_WrongPassword_DoesNotLeakBanStatus()
        {
            GivenPasswordLogin(MakeAccount(AccountStatuses.Banned, "Spam"), passwordOk: false);

            var r = await Sut().LoginAsync("a@x.com", "bad", true);

            Assert.Equal(AuthError.InvalidCredentials, r.Error);
            Assert.Null(r.BanReason);
        }

        [Fact]
        public async Task Login_PendingVerify_ReturnsEmailNotVerified()
        {
            GivenPasswordLogin(MakeAccount(AccountStatuses.PendingVerify), passwordOk: true);

            var r = await Sut().LoginAsync("a@x.com", "pw", true);

            Assert.Equal(AuthError.EmailNotVerified, r.Error);
            VerifyNoTokenIssued();
        }

        // ======================= LOGIN: GOOGLE =======================

        private static GoogleUser GoogleUserOf(bool verified = true) =>
            new("sub-1", "a@x.com", verified, "Google Name", "http://pic");

        [Fact]
        public async Task Google_InvalidCredential_ReturnsInvalidGoogleToken()
        {
            _google.Setup(g => g.ValidateAsync("cred")).ReturnsAsync((GoogleUser?)null);

            var r = await Sut().LoginWithGoogleAsync("cred");

            Assert.Equal(AuthError.InvalidGoogleToken, r.Error);
        }

        [Fact]
        public async Task Google_UnverifiedEmail_IsRejected()
        {
            _google.Setup(g => g.ValidateAsync("cred")).ReturnsAsync(GoogleUserOf(verified: false));

            var r = await Sut().LoginWithGoogleAsync("cred");

            Assert.Equal(AuthError.GoogleEmailNotVerified, r.Error);
        }

        [Fact]
        public async Task Google_NewUser_CreatesActivePlayerAccount_AndIsPersistent()
        {
            _google.Setup(g => g.ValidateAsync("cred")).ReturnsAsync(GoogleUserOf());
            _accounts.Setup(a => a.FindByGoogleSubjectAsync("sub-1", It.IsAny<CancellationToken>())).ReturnsAsync((Account?)null);
            _accounts.Setup(a => a.FindByEmailAsync("a@x.com", It.IsAny<CancellationToken>())).ReturnsAsync((Account?)null);
            Account? created = null;
            _accounts.Setup(a => a.AddAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>()))
                     .Callback<Account, CancellationToken>((acc, _) => { acc.AccountId = 7; created = acc; })
                     .Returns(Task.CompletedTask);

            var r = await Sut().LoginWithGoogleAsync("cred");

            Assert.True(r.IsSuccess);
            Assert.NotNull(created);
            Assert.Equal("sub-1", created!.GoogleSubjectId);
            Assert.Equal(AccountRoles.Player, created.Role);
            Assert.Equal(AccountStatuses.Active, created.Status);
            Assert.NotNull(created.EmailVerifiedAt);
            Assert.Null(created.PasswordHash);
            Assert.True(r.Tokens!.Persistent);
            _accounts.Verify(a => a.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        }

        [Fact]
        public async Task Google_ExistingEmailAccount_IsLinked_AndPendingBecomesActive()
        {
            var existing = MakeAccount(AccountStatuses.PendingVerify);
            _google.Setup(g => g.ValidateAsync("cred")).ReturnsAsync(GoogleUserOf());
            _accounts.Setup(a => a.FindByGoogleSubjectAsync("sub-1", It.IsAny<CancellationToken>())).ReturnsAsync((Account?)null);
            _accounts.Setup(a => a.FindByEmailAsync("a@x.com", It.IsAny<CancellationToken>())).ReturnsAsync(existing);

            var r = await Sut().LoginWithGoogleAsync("cred");

            Assert.True(r.IsSuccess);
            Assert.Equal("sub-1", existing.GoogleSubjectId);
            Assert.Equal(AccountStatuses.Active, existing.Status);
            Assert.NotNull(existing.EmailVerifiedAt);
            _accounts.Verify(a => a.AddAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Google_ExistingGoogleAccount_DoesNotCreateDuplicate()
        {
            var existing = MakeAccount(googleSub: "sub-1");
            _google.Setup(g => g.ValidateAsync("cred")).ReturnsAsync(GoogleUserOf());
            _accounts.Setup(a => a.FindByGoogleSubjectAsync("sub-1", It.IsAny<CancellationToken>())).ReturnsAsync(existing);

            var r = await Sut().LoginWithGoogleAsync("cred");

            Assert.True(r.IsSuccess);
            _accounts.Verify(a => a.AddAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Google_BannedAccount_IsBlockedWithReason()
        {
            var existing = MakeAccount(AccountStatuses.Banned, "Gian lận", googleSub: "sub-1");
            _google.Setup(g => g.ValidateAsync("cred")).ReturnsAsync(GoogleUserOf());
            _accounts.Setup(a => a.FindByGoogleSubjectAsync("sub-1", It.IsAny<CancellationToken>())).ReturnsAsync(existing);

            var r = await Sut().LoginWithGoogleAsync("cred");

            Assert.Equal(AuthError.AccountBanned, r.Error);
            Assert.Equal("Gian lận", r.BanReason);
            VerifyNoTokenIssued();
        }

        // ======================= REFRESH =======================

        [Fact]
        public async Task Refresh_InvalidToken_ReturnsInvalidRefreshToken()
        {
            _refresh.Setup(r => r.ConsumeAsync("tok", It.IsAny<CancellationToken>())).ReturnsAsync(ConsumeResult.Fail());

            var r = await Sut().RefreshAsync("tok");

            Assert.Equal(AuthError.InvalidRefreshToken, r.Error);
            VerifyNoTokenIssued();
        }

        [Fact]
        public async Task Refresh_AccountDeleted_ReturnsAccountNotFound()
        {
            _refresh.Setup(r => r.ConsumeAsync("tok", It.IsAny<CancellationToken>())).ReturnsAsync(new ConsumeResult(true, 1, true));
            _accounts.Setup(a => a.FindByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync((Account?)null);

            var r = await Sut().RefreshAsync("tok");

            Assert.Equal(AuthError.AccountNotFound, r.Error);
        }

        [Fact]
        public async Task Refresh_AccountBannedAfterLogin_RevokesAllTokens_AndIssuesNothing()
        {
            _refresh.Setup(r => r.ConsumeAsync("tok", It.IsAny<CancellationToken>())).ReturnsAsync(new ConsumeResult(true, 1, true));
            _accounts.Setup(a => a.FindByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(MakeAccount(AccountStatuses.Banned, "Spam"));

            var r = await Sut().RefreshAsync("tok");

            Assert.Equal(AuthError.AccountBanned, r.Error);
            Assert.Equal("Spam", r.BanReason);
            _refresh.Verify(x => x.RevokeAllAsync(1, It.IsAny<CancellationToken>()), Times.Once);
            VerifyNoTokenIssued();
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task Refresh_Success_KeepsRememberModeOfOldToken(bool persistent)
        {
            _refresh.Setup(r => r.ConsumeAsync("tok", It.IsAny<CancellationToken>())).ReturnsAsync(new ConsumeResult(true, 1, persistent));
            _accounts.Setup(a => a.FindByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(MakeAccount());

            var r = await Sut().RefreshAsync("tok");

            Assert.True(r.IsSuccess);
            Assert.Equal(persistent, r.Tokens!.Persistent);
            _refresh.Verify(x => x.CreateAsync(1, persistent, It.IsAny<CancellationToken>()), Times.Once);
        }

        // ======================= LOGOUT =======================

        [Fact]
        public async Task Logout_WithToken_RevokesIt()
        {
            await Sut().LogoutAsync("tok");

            _refresh.Verify(r => r.RevokeAsync("tok", It.IsAny<CancellationToken>()), Times.Once);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public async Task Logout_WithoutToken_DoesNothing(string? token)
        {
            await Sut().LogoutAsync(token);

            _refresh.Verify(r => r.RevokeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
