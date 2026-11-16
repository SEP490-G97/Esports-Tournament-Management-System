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

    // MAT-05 / MAT-06
    Task<Result<int>> RequestSubstituteAsync(int matchId, int requestedByAccountId, CreateSubstituteRequest request);
    Task<Result<IReadOnlyList<SubstituteRequestDto>>> GetSubstituteRequestsAsync(int matchId, int viewerAccountId);
    Task<Result> DecideSubstituteAsync(int matchRequestId, int decidedByAccountId, bool approve);

    // MAT-07
    Task<Result<IReadOnlyList<ChatMessageDto>>> GetChatMessagesAsync(int matchId, int viewerAccountId);
    Task<Result<ChatMessageDto>> SendChatMessageAsync(int matchId, int senderAccountId, SendChatMessageRequest request);

    // MAT-09
    Task<Result> ProcessWalkoverAsync(int matchId, int actorAccountId, WalkoverRequest request);

    // MAT-10
    Task<Result> RematchAsync(int matchId, int actorAccountId, RematchRequest request);

    // MAT-11
    Task<Result> DisqualifyAsync(int matchId, int actorAccountId, DisqualifyRequest request);

    // MAT-12
    Task<Result> ForfeitAsync(int matchId, int captainAccountId, ForfeitRequest request);
}
