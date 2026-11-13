using ETOMS_API.DTOs.Admin;
using ETOMS_API.Utils;

namespace ETOMS_API.Services.Interface
{
    public interface IAdminAccountService
    {
        Task<AdminListResult> ListAsync(AccountListQuery query, CancellationToken ct = default);
        Task<AdminResult> BanAsync(int actorId, int targetId, string? reason, CancellationToken ct = default);
        Task<AdminResult> UnbanAsync(int actorId, int targetId, string? note, CancellationToken ct = default);
    }
}
