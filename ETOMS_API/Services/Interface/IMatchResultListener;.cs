namespace ETOMS_API.Services.Interface
{
    /// <summary>Được gọi SAU KHI một trận đã có kết quả cuối (vd xử thắng trắng) để chạy các bước tiếp theo (đưa đội đi tiếp nhánh...).</summary>
    public interface IMatchResultListener
    {
        Task OnResultAsync(int matchId, CancellationToken ct);

        /// <summary>
        /// Gọi TRƯỚC khi kết quả của trận bị gỡ (vd BTC gỡ cờ No-show) để hoàn tác bước đã làm ở <see cref="OnResultAsync"/>.
        /// Ném <see cref="ResultRollbackBlockedException"/> nếu không thể hoàn tác (vd trận kế tiếp đã bắt đầu).
        /// </summary>
        Task OnResultRevertAsync(int matchId, CancellationToken ct);
    }
}
