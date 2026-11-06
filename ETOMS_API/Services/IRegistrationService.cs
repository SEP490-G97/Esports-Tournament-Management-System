using ETOMS_API.Utils;
using ETOMS_API.DTOs.Registration;

namespace ETOMS_API.Services;

public interface IRegistrationService
{
    // Danh sách giải đang mở đăng ký (cho dropdown nộp đơn).
    Task<IReadOnlyList<OpenTournamentDto>> GetOpenTournamentsAsync();

    // STUB TẠM thay REG-01: nộp đơn đăng ký tối thiểu.
    Task<Result<int>> CreateRegistrationAsync(CreateRegistrationRequest request, int accountId);

    // REG-04: đơn của đội mà người dùng đang làm đội trưởng.
    Task<IReadOnlyList<MyRegistrationDto>> GetMyRegistrationsAsync(int accountId);

    // REG-03: tải minh chứng lệ phí. Chỉ đội trưởng của đơn đó.
    Task<Result> UploadPaymentProofAsync(int registrationId, int accountId, string fileUrl, string? note);

    // STUB TẠM thay REG-06/07: danh sách đơn PENDING của 1 giải + duyệt/từ chối.
    Task<IReadOnlyList<PendingRegistrationDto>> GetPendingRegistrationsAsync(int tournamentId);
    Task<Result> ApproveRegistrationAsync(int registrationId, int organizerAccountId);
    Task<Result> RejectRegistrationAsync(int registrationId, int organizerAccountId, string reason);

    Task<bool> IsOrganizerAsync(int tournamentId, int accountId);
}
