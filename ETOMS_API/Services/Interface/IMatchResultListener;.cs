namespace ETOMS_API.Services.Interface
{
    /// <summary>Được gọi SAU KHI một trận đã có kết quả cuối (vd xử thắng trắng) để chạy các bước tiếp theo (đưa đội đi tiếp nhánh...).</summary>
    public interface IMatchResultListener
    {
        Task OnResultAsync(int matchId, CancellationToken ct);
    }
}
