using ETOMS_API.DTOs.Auth;
using ETOMS_API.Services.Interface;
using Google.Apis.Auth;
using static ETOMS_API.DTOs.Auth.AuthDtos;

namespace ETOMS_API.Services.Impl
{
    public class GoogleTokenValidator(IConfiguration cfg) : IGoogleTokenValidator
    {
        public async Task<GoogleUser?> ValidateAsync(string credential)
        {
            try
            {
                var p = await GoogleJsonWebSignature.ValidateAsync(credential,
                    new GoogleJsonWebSignature.ValidationSettings
                    {
                        Audience = [cfg["Google:ClientId"]!]
                    });
                return new GoogleUser(p.Subject, p.Email, p.EmailVerified, p.Name, p.Picture);
            }
            catch (InvalidJwtException)
            {
                return null;
            }
        }
    }
}
