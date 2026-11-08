using ETOMS_API.DTOs.GameRule;
using ETOMS_API.Utils;

namespace ETOMS_API.Services.Interface;

// GRL-01 / UC-43 bước 5: System Admin khai luật thi đấu của từng tựa game.
public interface IGameRuleService
{
    Task<bool> IsAdminAsync(int accountId);

    Task<IReadOnlyList<GameRuleDto>> GetRulesAsync();

    Task<Result<GameRuleDto>> UpdateRuleAsync(int gameId, UpdateGameRuleRequest request);
}
