using ETOMS_API.DTOs;
using ETOMS_API.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace ETOMS_API.Hubs;

/// <summary>
/// SignalR hub phòng chờ trận (MAT-07). Client: <c>JoinMatch(matchId)</c> rồi <c>Send(matchId, text)</c>; nhận sự kiện <c>message</c>.
/// Mọi tin nhắn được lưu qua <see cref="IMatchChatService"/> TRƯỚC khi phát — không có tin nào tới người nhận mà không nằm trong log.
/// </summary>
[Authorize]
public class MatchChatHub : Hub
{
    public const string Route = "/hubs/match-chat";
    public static string Group(int matchId) => "match-chat-" + matchId;

    private readonly IMatchChatService _chat;

    public MatchChatHub(IMatchChatService chat) => _chat = chat;

    private (int Id, string? Role) Actor()
    {
        var u = Context.User;
        var raw = u?.FindFirstValue("sub") ?? u?.FindFirstValue(ClaimTypes.NameIdentifier);
        var role = u?.FindFirstValue("role") ?? u?.FindFirstValue(ClaimTypes.Role);
        if (!int.TryParse(raw, out var id) || id <= 0) throw new HubException("UNAUTHORIZED");
        return (id, role);
    }

    public static string Code(ChatError e) => e switch
    {
        ChatError.NotFound => "NOT_FOUND",
        ChatError.Forbidden => "FORBIDDEN",
        ChatError.LobbyClosed => "LOBBY_CLOSED",
        ChatError.EmptyMessage => "EMPTY_MESSAGE",
        ChatError.TooLong => "TOO_LONG",
        ChatError.RateLimited => "RATE_LIMITED",
        _ => "ERROR"
    };

    /// <summary>Vào phòng; trả về vai trò của người gọi. Ném HubException(code) nếu không có quyền.</summary>
    public async Task<string> JoinMatch(int matchId)
    {
        var (id, role) = Actor();
        var r = await _chat.AuthorizeAsync(matchId, id, role, Context.ConnectionAborted);
        if (r.Error != ChatError.None) throw new HubException(Code(r.Error));
        await Groups.AddToGroupAsync(Context.ConnectionId, Group(matchId));
        return r.Data!;
    }

    public Task LeaveMatch(int matchId) => Groups.RemoveFromGroupAsync(Context.ConnectionId, Group(matchId));

    /// <summary>Gửi tin: lưu DB rồi phát cho cả phòng. Trả về tin đã lưu cho người gửi.</summary>
    public async Task<ChatMessageDto> Send(int matchId, string? text)
    {
        var (id, role) = Actor();
        var r = await _chat.SendAsync(matchId, id, role, text, Context.ConnectionAborted);
        if (r.Error != ChatError.None) throw new HubException(Code(r.Error));
        await Clients.Group(Group(matchId)).SendAsync("message", r.Data!);
        return r.Data!;
    }
}