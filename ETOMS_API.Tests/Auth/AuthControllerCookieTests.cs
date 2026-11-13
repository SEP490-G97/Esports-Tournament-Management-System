using ETOMS_API.Controllers;
using ETOMS_API.Services.Interface;
using ETOMS_API.Utils;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;
using static ETOMS_API.DTOs.Auth.AuthDtos;

namespace ETOMS_API.Tests.Auth
{
    public class AuthControllerCookieTests
    {
        private readonly Mock<IAuthService> _auth = new();

        private AuthController Sut()
        {
            var env = new Mock<IWebHostEnvironment>();
            env.SetupGet(e => e.EnvironmentName).Returns("Development");
            return new AuthController(_auth.Object, env.Object)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
            };
        }

        private static AuthResult Success(bool persistent) => AuthResult.Ok(new AuthTokens(
            "access", "raw-refresh", DateTime.UtcNow.AddDays(7), persistent,
            new AccountDto(1, "a@x.com", "A", null, AccountRoles.Player)));

        private static string SetCookie(AuthController c) => c.Response.Headers["Set-Cookie"].ToString();

        [Fact]
        public async Task Login_Remember_SetsPersistentHttpOnlyCookie()
        {
            _auth.Setup(a => a.LoginAsync("a@x.com", "pw", true, It.IsAny<CancellationToken>())).ReturnsAsync(Success(true));
            var c = Sut();

            var result = await c.Login(new LoginRequest("a@x.com", "pw", true), default);

            Assert.IsType<OkObjectResult>(result);
            var h = SetCookie(c).ToLowerInvariant();
            Assert.Contains("refresh_token=raw-refresh", h);
            Assert.Contains("httponly", h);
            Assert.Contains("path=/auth", h);
            Assert.Contains("expires=", h); // cookie lưu lâu dài
        }

        [Fact]
        public async Task Login_NotRemember_SetsSessionCookieWithoutExpires()
        {
            _auth.Setup(a => a.LoginAsync("a@x.com", "pw", false, It.IsAny<CancellationToken>())).ReturnsAsync(Success(false));
            var c = Sut();

            await c.Login(new LoginRequest("a@x.com", "pw", false), default);

            var h = SetCookie(c).ToLowerInvariant();
            Assert.Contains("refresh_token=raw-refresh", h);
            Assert.DoesNotContain("expires=", h); // cookie phiên: mất khi đóng trình duyệt
            Assert.DoesNotContain("max-age=", h);
        }

        [Fact]
        public async Task Login_RememberOmittedInRequest_DefaultsToTrue()
        {
            Assert.True(new LoginRequest("a@x.com", "pw").Remember);
            await Task.CompletedTask;
        }

        [Fact]
        public async Task Login_Banned_Returns403WithCodeAndReason_AndSetsNoCookie()
        {
            _auth.Setup(a => a.LoginAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(AuthResult.Fail(AuthError.AccountBanned, "Spam"));
            var c = Sut();

            var result = await c.Login(new LoginRequest("a@x.com", "pw", true), default);

            var obj = Assert.IsType<ObjectResult>(result);
            Assert.Equal(403, obj.StatusCode);
            Assert.Contains("ACCOUNT_BANNED", obj.Value!.ToString());
            Assert.Contains("Spam", obj.Value!.ToString());
            Assert.Equal(0, c.Response.Headers["Set-Cookie"].Count);
        }

        [Fact]
        public async Task Login_InvalidCredentials_Returns401()
        {
            _auth.Setup(a => a.LoginAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(AuthResult.Fail(AuthError.InvalidCredentials));

            var result = await Sut().Login(new LoginRequest("a@x.com", "bad", true), default);

            Assert.IsType<UnauthorizedObjectResult>(result);
        }

        [Fact]
        public async Task Refresh_WithoutCookie_Returns401_AndDoesNotCallService()
        {
            var result = await Sut().Refresh(default);

            Assert.IsType<UnauthorizedObjectResult>(result);
            _auth.Verify(a => a.RefreshAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Refresh_SessionTokenStaysSessionCookie()
        {
            _auth.Setup(a => a.RefreshAsync("old", It.IsAny<CancellationToken>())).ReturnsAsync(Success(persistent: false));
            var c = Sut();
            c.Request.Headers["Cookie"] = "refresh_token=old";

            await c.Refresh(default);

            Assert.DoesNotContain("expires=", SetCookie(c).ToLowerInvariant());
        }
    }
}