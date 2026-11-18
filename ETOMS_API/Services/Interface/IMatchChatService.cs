
using ETOMS_API.DTOs;

namespace ETOMS_API.Services.Interface;

public enum ChatError { None, NotFound, Forbidden, LobbyClosed, EmptyMessage, TooLong, RateLimited }

public record ChatResult<T>(ChatError Error, T? Data = default);

public interface IMatchChatService
{
    /// <summary>Kiểm tra quyền vào phòng (đội trưởng 2 đội / BTC của giải / trọng tài của trận / admin chỉ đọc).</summary>
    Task<ChatResult<string>> AuthorizeAsync(int matchId, int actorId, string? role, CancellationToken ct = default);

    /// <summary>Lịch sử: <paramref name="limit"/> tin mới nhất (hoặc cũ hơn <paramref name="beforeId"/>).</summary>
    Task<ChatResult<ChatHistory>> HistoryAsync(int matchId, int actorId, string? role, long? beforeId = null, int limit = 100, CancellationToken ct = default);

    /// <summary>Lưu tin nhắn (bất biến) rồi trả về bản đã lưu để hub phát cho cả phòng.</summary>
    Task<ChatResult<ChatMessageDto>> SendAsync(int matchId, int actorId, string? role, string? text, CancellationToken ct = default);

    /// <summary>Toàn bộ log (tăng dần) để xuất làm bằng chứng — BTC của giải / trọng tài của trận / admin.</summary>
    Task<ChatResult<IReadOnlyList<ChatMessageDto>>> ExportAsync(int matchId, int actorId, string? role, CancellationToken ct = default);
}