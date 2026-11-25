using ETOMS_API.Models;

namespace ETOMS_API.Services.Interface
{
    public interface ITokenService
    {
        string CreateAccessToken(Account account);
        string GenerateRefreshToken();
        string HashToken(string token);
    }
}
