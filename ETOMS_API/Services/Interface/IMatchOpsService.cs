using ETOMS_API.DTOs.Match;
using ETOMS_API.Utils;

namespace ETOMS_API.Services.Interface;

// STUB TẠM thay module MAT (Vũ Đức Hải: MAT-01/04/05/06/07, Nguyễn Quang Anh: MAT-08..12).
public interface IMatchOpsService
{
    Task<MatchDetailDto?> GetMatchDetailAsync(int matchId, int? viewerAccountId);

    // MAT-04
    Task<Result> ManualCheckInAsync(int matchId, int actorAccountId, ManualCheckInRequest request);

    // MAT-08
    Task<Result> SetRoomInfoAsync(int matchId, int actorAccountId, SetRoomInfoRequest request);
    Task<Result> SelfCheckInAsync(int matchId, int captainAccountId);

    // MAT-05 / MAT-06
    Task<Result<int>> RequestSubstituteAsync(int matchId, int requestedByAccountId, CreateSubstituteRequest request);
    Task<Result<IReadOnlyList<SubstituteRequestDto>>> GetSubstituteRequestsAsync(int matchId, int viewerAccountId);
    Task<Result> DecideSubstituteAsync(int matchRequestId, int decidedByAccountId, bool approve);

    // MAT-07
    Task<Result<MatchLobbyDto>> GetChatMessagesAsync(int matchId, int viewerAccountId);
    Task<Result<ChatMessageDto>> SendChatMessageAsync(int matchId, int senderAccountId, SendChatMessageRequest request);
    Task<Result> ReportChatMessageAsync(int matchId, int messageId, int reporterAccountId, ReportChatMessageRequest request);

    // MAT-09
    Task<Result> ProcessWalkoverAsync(int matchId, int actorAccountId, WalkoverRequest request);

    // MAT-10
    Task<Result> RematchAsync(int matchId, int actorAccountId, RematchRequest request);

    // MAT-11
    Task<Result> DisqualifyAsync(int matchId, int actorAccountId, DisqualifyRequest request);

    // MAT-12
    Task<Result> ForfeitAsync(int matchId, int captainAccountId, ForfeitRequest request);

    // Hủy trận, xử thua cả hai đội: không đội nào đi tiếp; đối thủ ở trận sau thắng trắng.
    Task<Result> CancelMatchAsync(int matchId, int actorAccountId, CancelMatchRequest request);

    // UC-26 bước 3 / A2: các trận phía sau sẽ bị hủy kết quả nếu cho trận này đấu lại.
    Task<Result<List<RematchImpactDto>>> PreviewRematchAsync(int matchId, int actorAccountId);

    // NUI-MAT-02 (Report 3 §3.1.4, MF-08): tới giờ thi đấu (hết cửa sổ điểm danh) mà chỉ một đội điểm danh → đội kia vắng mặt,
    // trận xử thắng trắng cho đội có mặt và đẩy lên vòng trên. Trả số trận đã xử.
    Task<int> ProcessNoShowsAsync(DateTime nowUtc, CancellationToken ct = default);
}
