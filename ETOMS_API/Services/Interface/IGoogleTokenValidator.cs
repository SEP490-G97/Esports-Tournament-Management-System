using static ETOMS_API.DTOs.Auth.AuthDtos;

namespace ETOMS_API.Services.Interface
{
    public interface IGoogleTokenValidator
    {
        Task<GoogleUser?> ValidateAsync(string credential);
    }
}
