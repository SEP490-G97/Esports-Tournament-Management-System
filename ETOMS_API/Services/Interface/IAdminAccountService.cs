using ETOMS_API.DTOs.Admin;
using ETOMS_API.Utils;

namespace ETOMS_API.Services.Interface
{
    public interface IAdminAccountService
    {
        Task<AdminListResult> ListAsync(AccountListQuery query, CancellationToken ct = default);
        Task<AdminResult> BanAsync(int actorId, int targetId, string? reason, IReadOnlyList<ChairHandover>? handovers = null, bool confirmWithoutHandover = false, CancellationToken ct = default);
        Task<BanImpactResponse?> GetBanImpactAsync(int targetId, CancellationToken ct = default);
        Task<AdminResult> RequestChairHandoverAsync(int actorId, int targetId, string? reason, CancellationToken ct = default);
        Task<AdminResult> UnbanAsync(int actorId, int targetId, string? note, CancellationToken ct = default);
    }
}
