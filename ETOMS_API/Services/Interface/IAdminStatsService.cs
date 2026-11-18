using ETOMS_API.DTOs.Admin;

namespace ETOMS_API.Services.Interface
{
    public enum StatsError { None, InvalidRange }

    public record StatsResult(StatsError Error, StatsOverview? Data = null);

    public interface IAdminStatsService
    {
        /// <param name="days">Số ngày của biểu đồ người dùng mới (7..365).</param>
        /// <param name="tzOffsetMinutes">Chênh lệch múi giờ của người xem so với UTC, tính bằng phút (ví dụ Việt Nam = 420).</param>
        Task<StatsResult> GetOverviewAsync(int days, int tzOffsetMinutes, CancellationToken ct = default);
    }
}
