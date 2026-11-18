using ETOMS_API.DTOs.Tournament;
using ETOMS_API.Utils;

namespace ETOMS_API.Services.Interface;

// Vòng đời giải (mục 28): bắt đầu / kết thúc theo việc thật, "Chưa đủ đội" và gia hạn đăng ký.
public interface ITournamentLifecycleService
{
    Task<int?> GetOrganizerAccountIdAsync(int tournamentId);
    Task<TournamentLifecycleDto?> GetAsync(int tournamentId);
    Task<Result<TournamentLifecycleDto>> StartAsync(int tournamentId, int organizerAccountId);
    Task<Result<TournamentLifecycleDto>> FinishAsync(int tournamentId, int organizerAccountId, IReadOnlyList<int>? tieOrder);
    Task<Result<TournamentLifecycleDto>> ExtendRegistrationAsync(int tournamentId, int organizerAccountId, ExtendRegistrationRequest request);
    // Chạy định kỳ: tự bắt đầu giải đã khóa nhánh khi tới giờ khai mạc, tự kết thúc giải đã xong mọi trận.
    Task<int> RunScheduledAsync(CancellationToken ct);
}
