using ETOMS_API.DTOs.GameRule;
using ETOMS_API.Utils;

namespace ETOMS_API.Services.Interface;

// GRL-03: Các vị trí trong đội của từng tựa game. Vị trí không xóa, chỉ chuyển sang "đã loại"
// để đội hình đã khai trước đó vẫn đọc được.
public interface IGamePositionService
{
    Task<Result<IReadOnlyList<GamePositionDto>>> GetPositionsAsync(int gameId);

    Task<Result<GamePositionDto>> AddPositionAsync(int gameId, SaveGamePositionRequest request);

    Task<Result<GamePositionDto>> UpdatePositionAsync(int gameId, int positionId, SaveGamePositionRequest request);

    Task<Result<IReadOnlyList<GamePositionDto>>> MovePositionAsync(int gameId, int positionId, string direction);
}
