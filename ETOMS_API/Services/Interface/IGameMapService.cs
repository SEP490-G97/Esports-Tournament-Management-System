using ETOMS_API.DTOs.GameRule;
using ETOMS_API.Utils;

namespace ETOMS_API.Services.Interface;

// GRL-02: Kho bản đồ của từng bộ luật. Bản đồ không xóa, chỉ chuyển sang "đã loại".
public interface IGameMapService
{
    Task<Result<IReadOnlyList<GameMapDto>>> GetMapsAsync(int rulesetId);

    Task<Result<GameMapDto>> AddMapAsync(int rulesetId, SaveGameMapRequest request, string? imageUrl);

    Task<Result<GameMapDto>> UpdateMapAsync(int rulesetId, int mapId, SaveGameMapRequest request, string? newImageUrl);
}
