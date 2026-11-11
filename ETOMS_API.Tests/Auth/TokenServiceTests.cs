using ETOMS_API.Models;
using ETOMS_API.Services.Impl;
using ETOMS_API.Utils;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Xunit;

namespace ETOMS_API.Tests.Auth
{
    public class TokenServiceTests
    {
        private readonly TokenService _sut = new(Options.Create(new JwtOptions
        {
            Key = "test-key-test-key-test-key-test-key-1234",
            Issuer = "test-issuer",
            Audience = "test-audience",
            AccessMinutes = 15,
        }));

        [Fact]
        public void HashToken_IsDeterministic_64HexChars_AndDifferentPerInput()
        {
            var h1 = _sut.HashToken("abc");
            Assert.Equal(h1, _sut.HashToken("abc"));
            Assert.Equal(64, h1.Length);
            Assert.NotEqual(h1, _sut.HashToken("abd"));
        }

        [Fact]
        public void GenerateRefreshToken_IsUniqueAndLongEnough()
        {
            var a = _sut.GenerateRefreshToken();
            var b = _sut.GenerateRefreshToken();
            Assert.NotEqual(a, b);
            Assert.True(a.Length >= 80);
        }

        [Fact]
        public void CreateAccessToken_ContainsSubRoleEmail_AndExpiresIn15Minutes()
        {
            var jwt = new JsonWebTokenHandler().ReadJsonWebToken(_sut.CreateAccessToken(new Account
            {
                AccountId = 42,
                Email = "a@x.com",
                Role = AccountRoles.Admin,
            }));

            Assert.Equal("42", jwt.GetClaim("sub").Value);
            Assert.Equal(AccountRoles.Admin, jwt.GetClaim("role").Value);
            Assert.Equal("a@x.com", jwt.GetClaim("email").Value);
            Assert.Equal("test-issuer", jwt.Issuer);
            Assert.InRange((jwt.ValidTo - DateTime.UtcNow).TotalMinutes, 14, 15.1);
        }
    }
}