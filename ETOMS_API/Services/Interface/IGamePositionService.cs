using ETOMS_API.DTOs.GameRule;
using ETOMS_API.Utils;

namespace ETOMS_API.Services.Interface;

// GRL-03: Các vị trí trong đội của từng bộ luật. Vị trí không xóa, chỉ chuyển sang "đã loại"
// để đội hình đã khai trước đó vẫn đọc được.
public interface IGamePositionService
{
    Task<Result<IReadOnlyList<GamePositionDto>>> GetPositionsAsync(int rulesetId);

    Task<Result<GamePositionDto>> AddPositionAsync(int rulesetId, SaveGamePositionRequest request);

    Task<Result<GamePositionDto>> UpdatePositionAsync(int rulesetId, int positionId, SaveGamePositionRequest request);

    Task<Result<IReadOnlyList<GamePositionDto>>> MovePositionAsync(int rulesetId, int positionId, string direction);
}
