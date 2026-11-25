using ETOMS_API.DTOs.GameRule;
using ETOMS_API.Utils;

namespace ETOMS_API.Services.Interface;

// GRL-01: System Admin tạo và sửa các bộ luật thi đấu của từng tựa game.
public interface IGameRuleService
{
    Task<bool> IsAdminAsync(int accountId);

    Task<GameRuleBoardDto> GetBoardAsync();

    Task<Result<GameRulesetDto>> CreateRulesetAsync(int gameId, SaveGameRulesetRequest request);

    Task<Result<GameRulesetDto>> UpdateRulesetAsync(int rulesetId, SaveGameRulesetRequest request);
}
