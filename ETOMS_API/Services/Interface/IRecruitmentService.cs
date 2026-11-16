// Services/Interface/IRecruitmentService.cs
using ETOMS_API.DTOs.Recruitment;

namespace ETOMS_API.Services.Interface
{
    public interface IRecruitmentService
    {
        Task<int> CreateLFPPostAsync(int accountId, CreateLFPPostDto dto);
        Task<int> CreateLFTPostAsync(int accountId, CreateLFTPostDto dto);
        Task<object> GetRecruitmentBoardAsync(RecruitmentBoardQueryDto query);
        Task<bool> ApplyToLFPAsync(int accountId, ApplyLFPPostDto dto);
        Task<bool> ReviewApplicationAsync(int captainAccountId, int requestId, ReviewApplicationDto dto);
        Task<bool> InviteFreeAgentAsync(int captainAccountId, InviteFreeAgentDto dto);
    }
}