using ETOMS_API.DTOs.Admin;
using ETOMS_API.Utils;

namespace ETOMS_API.Services.Interface;

// UC-43: đơn xin làm Ban tổ chức và việc Admin cấp tài khoản BTC riêng.
public interface IOrganizerRequestService
{
    Task<MyOrganizerRequestDto?> GetMineAsync(int accountId, CancellationToken ct = default);
    Task<Result<MyOrganizerRequestDto>> SubmitAsync(int accountId, SubmitOrganizerRequest request, CancellationToken ct = default);
    Task<Result<MyOrganizerRequestDto>> CancelAsync(int accountId, CancellationToken ct = default);
    Task<Result<RevealedOrganizerPassword>> RevealOrganizerPasswordAsync(int accountId, CancellationToken ct = default);
    Task<Result> RequestPasswordReissueAsync(int accountId, CancellationToken ct = default);
    Task<IReadOnlyList<OrganizerReissueItemDto>> ListReissueRequestsAsync(CancellationToken ct = default);
    Task<Result> ApprovePasswordReissueAsync(int adminAccountId, int requesterAccountId, CancellationToken ct = default);

    Task<IReadOnlyList<OrganizerRequestItemDto>> ListPendingAsync(CancellationToken ct = default);
    Task<Result<ApproveOrganizerResult>> ApproveAsync(int adminAccountId, int requesterAccountId, ApproveOrganizerRequest request, CancellationToken ct = default);
    Task<Result> RejectAsync(int adminAccountId, int requesterAccountId, RejectOrganizerRequest request, CancellationToken ct = default);
}
