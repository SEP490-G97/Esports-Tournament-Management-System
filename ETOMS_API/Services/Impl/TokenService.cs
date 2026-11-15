using ETOMS_API.Models;
using ETOMS_API.Services.Interface;
using ETOMS_API.Utils;
using Google.Protobuf.WellKnownTypes;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace ETOMS_API.Services.Impl
{
    public class TokenService(IOptions<JwtOptions> jwtOptions) : ITokenService
    {
        private readonly JwtOptions _o = jwtOptions.Value;

        public string CreateAccessToken(Account account)
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_o.Key));
            var descriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(
                [
                    new Claim("sub", account.AccountId.ToString()),
                new Claim("email", account.Email),
                new Claim("role", account.Role),
            ]),
                Issuer = _o.Issuer,
                Audience = _o.Audience,
                Expires = DateTime.UtcNow.AddMinutes(_o.AccessMinutes),
                SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256),
            };
            return new JsonWebTokenHandler().CreateToken(descriptor);
        }

        public string GenerateRefreshToken() => WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(64));

        public string HashToken(string token) =>
         Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }
}
