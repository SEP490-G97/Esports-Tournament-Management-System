using ETOMS_API.DTOs.Tournament;
using ETOMS_API.Utils;

namespace ETOMS_API.Services.Interface;

// GRL-06: Ban tổ chức chọn một bộ luật của tựa game cho giải.
public interface ITournamentRulesetService
{
    // null = không có giải.
    Task<int?> GetOrganizerAccountIdAsync(int tournamentId);

    Task<Result<TournamentRulesetBoardDto>> GetBoardAsync(int tournamentId);

    Task<Result<TournamentRulesetBoardDto>> SelectRulesetAsync(int tournamentId, int gameRulesetId);
}
