using ETOMS_API.DTOs.Bracket;

namespace ETOMS_API.Services.Interface;

public interface IThirdPlaceService
{
    // Chỉ đọc: trận tranh hạng Ba đang ở đâu.
    Task<ThirdPlaceStatusDto?> GetStatusAsync(int tournamentId);

    // Tạo trận nếu chưa có và đưa đội thua bán kết vào. Gọi lại nhiều lần không sinh thêm gì.
    Task<ThirdPlaceStatusDto?> SyncAsync(int tournamentId);

    // Tác vụ chạy nền: đồng bộ mọi giải đang bật tranh hạng Ba. Trả về số giải có thay đổi.
    Task<int> RunScheduledAsync(CancellationToken cancellationToken);
}
