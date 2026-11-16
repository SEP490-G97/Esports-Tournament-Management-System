// Services/Interface/ITournamentSetupService.cs
using ETOMS_API.DTOs.TournamentSetup;

namespace ETOMS_API.Services.Interface
{
    public interface ITournamentSetupService
    {
        Task<int> CreateStep1GeneralInfoAsync(int organizerAccountId, Step1GeneralInfoDto dto);
        Task<bool> UpdateStep2TimelineAsync(int tournamentId, Step2TimelineDto dto);
        Task<bool> UpdateStep3BoConfigAsync(int tournamentId, Step3BoConfigDto dto);
        Task<bool> UpdateStep4FinanceAsync(int tournamentId, Step4FinanceDto dto);
        Task<bool> FinalizeStep5PublishAsync(int tournamentId, Step5RequirementDto dto);

        // Quyền tạo giải: chỉ ORGANIZER/ADMIN. STUB TẠM thay module TOUR (Nguyễn Quang Anh) —
        // commit trước chưa kiểm quyền nên Player vẫn tạo được giải, bổ sung lại cho đúng.
        Task<bool> IsOrganizerOrAdminAsync(int accountId);

        // Quyền sửa/công bố 1 giải đã có: ADMIN, hoặc đúng người đã tạo giải đó.
        Task<bool?> CanManageAsync(int tournamentId, int accountId);
    }
}