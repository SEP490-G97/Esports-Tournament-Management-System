namespace ETOMS_API.Services.Interface;

public record NoShowScanResult(int Scanned, int Walkovers, int DoubleNoShows, IReadOnlyList<int> ListenerFailures);

public class ResultRollbackBlockedException : Exception
{
    public ResultRollbackBlockedException(string message) : base(message) { }
}

/// <summary>
/// Quét các trận READY đã quá giờ thi đấu mà có đội chưa check-in:
/// một đội vắng → gắn cờ No-show và xử thắng trắng (WALKOVER) cho đội còn lại;
/// cả hai vắng → không thể chọn đội thắng nên chuyển DISPUTED kèm một Dispute (REPORT_TIMEOUT) để BTC quyết định.
/// </summary>
public interface INoShowService
{
    Task<NoShowScanResult> ScanAsync(DateTime? nowUtc = null, CancellationToken ct = default);
}