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
    }
}