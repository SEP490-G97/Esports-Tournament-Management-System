namespace ETOMS_API.Services.Interface
{
    public interface IAccountBanService
    {
        Task<bool> BanAsync(int accountId, string reason, CancellationToken ct = default);
        Task<bool> UnbanAsync(int accountId, CancellationToken ct = default);
    }
}
