
using ETOMS_API.DTOs;
using ETOMS_API.Models;
using ETOMS_API.Repositories;
using ETOMS_API.Services.Interface;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace ETOMS_API.Services.Impl;

/// <summary>
/// Phòng chờ chat theo trận (MAT-07). Thành viên: đội trưởng 2 đội, BTC của giải, trọng tài của trận.
/// Admin chỉ được đọc. Tin nhắn chỉ thêm, không sửa / xoá — log dùng làm bằng chứng khi xử tranh chấp.
/// </summary>
public class MatchChatService : IMatchChatService
{
    public const int MaxLength = 1000;
    public const int DefaultPage = 100;
    public const int MaxPage = 200;
    public const int BurstLimit = 8;                       // tối đa 8 tin / 10 giây / người gửi
    public static readonly TimeSpan BurstWindow = TimeSpan.FromSeconds(10);

    private static readonly string[] WritableStatuses = { "READY", "LIVE", "WAITING_REPORT", "DISPUTED" };

    private readonly EtomsDbContext _db;
    private readonly TimeProvider _clock;

    public MatchChatService(EtomsDbContext db, TimeProvider? clock = null)
    {
        _db = db;
        _clock = clock ?? TimeProvider.System;
    }

    private DateTime Now => _clock.GetUtcNow().UtcDateTime;

    private sealed class Ctx
    {
        public Match Match = null!;
        public Tournament Tournament = null!;
        public Team? TeamA, TeamB;
        public int? Referee;
        public int Organizer;
    }

    private async Task<Ctx?> LoadAsync(int matchId, CancellationToken ct)
    {
        var m = await _db.Set<Match>().AsNoTracking().FirstOrDefaultAsync(x => x.MatchId == matchId, ct);
        if (m == null) return null;
        var t = await _db.Set<Tournament>().AsNoTracking().FirstOrDefaultAsync(x => x.TournamentId == m.TournamentId, ct);
        if (t == null) return null;
        var ids = new[] { m.TeamAid, m.TeamBid }.Where(x => x != null).Select(x => Convert.ToInt32(x)).ToList();
        var teams = ids.Count == 0 ? new List<Team>() : await _db.Set<Team>().AsNoTracking().Where(x => ids.Contains(x.TeamId)).ToListAsync(ct);
        return new Ctx
        {
            Match = m,
            Tournament = t,
            TeamA = teams.FirstOrDefault(x => x.TeamId == m.TeamAid),
            TeamB = teams.FirstOrDefault(x => x.TeamId == m.TeamBid),
            Referee = m.RefereeAccountId == null ? null : Convert.ToInt32(m.RefereeAccountId),
            Organizer = Convert.ToInt32(t.OrganizerAccountId)
        };
    }

    /// <summary>Vai trò của người dùng trong phòng; null = không có quyền.</summary>
    private static (string Role, int? TeamId)? Resolve(Ctx c, int actor, string? role)
    {
        if (c.Organizer == actor) return ("ORGANIZER", null);
        if (c.Referee == actor) return ("REFEREE", null);
        if (c.TeamA != null && Convert.ToInt32(c.TeamA.CaptainAccountId) == actor) return ("CAPTAIN", c.TeamA.TeamId);
        if (c.TeamB != null && Convert.ToInt32(c.TeamB.CaptainAccountId) == actor) return ("CAPTAIN", c.TeamB.TeamId);
        if (role == "ADMIN") return ("ADMIN", null);
        return null;
    }

    private static bool IsWritable(Ctx c) =>
        c.TeamA != null && c.TeamB != null
        && WritableStatuses.Contains(c.Match.Status)
        && c.Tournament.Status != "CANCELLED" && c.Tournament.Status != "COMPLETED";

    public async Task<ChatResult<string>> AuthorizeAsync(int matchId, int actorId, string? role, CancellationToken ct = default)
    {
        var c = await LoadAsync(matchId, ct);
        if (c == null) return new(ChatError.NotFound);
        var who = Resolve(c, actorId, role);
        return who == null ? new(ChatError.Forbidden) : new(ChatError.None, who.Value.Role);
    }

    private async Task<Dictionary<int, string>> NamesAsync(IEnumerable<int> ids, CancellationToken ct)
    {
        var list = ids.Distinct().ToList();
        if (list.Count == 0) return new();
        var rows = await _db.Set<Account>().AsNoTracking().Where(a => list.Contains(a.AccountId)).ToListAsync(ct);
        return rows.ToDictionary(a => a.AccountId, a => a.FullName ?? ("#" + a.AccountId));
    }

    private static ChatMessageDto ToDto(MatchChatMessage m, Dictionary<int, string> names, Dictionary<int, string> teamNames)
    {
        int sender = Convert.ToInt32(m.SenderAccountId);
        int? team = m.TeamId == null ? null : Convert.ToInt32(m.TeamId);
        return new ChatMessageDto(Convert.ToInt64(m.MatchChatMessageId), Convert.ToInt32(m.MatchId), sender,
            names.TryGetValue(sender, out var n) ? n : "#" + sender, m.SenderRole, team,
            team != null && teamNames.TryGetValue(team.Value, out var tn) ? tn : null, m.Body, m.SentAt);
    }

    public async Task<ChatResult<ChatHistory>> HistoryAsync(int matchId, int actorId, string? role, long? beforeId = null, int limit = DefaultPage, CancellationToken ct = default)
    {
        var c = await LoadAsync(matchId, ct);
        if (c == null) return new(ChatError.NotFound);
        var who = Resolve(c, actorId, role);
        if (who == null) return new(ChatError.Forbidden);

        limit = Math.Clamp(limit, 1, MaxPage);
        var q = _db.Set<MatchChatMessage>().AsNoTracking().Where(x => x.MatchId == matchId);
        if (beforeId != null) q = q.Where(x => x.MatchChatMessageId < beforeId.Value);
        var rows = await q.OrderByDescending(x => x.MatchChatMessageId).Take(limit + 1).ToListAsync(ct);
        bool more = rows.Count > limit;
        rows = rows.Take(limit).OrderBy(x => x.MatchChatMessageId).ToList();

        var teamNames = new Dictionary<int, string>();
        if (c.TeamA != null) teamNames[c.TeamA.TeamId] = c.TeamA.Name;
        if (c.TeamB != null) teamNames[c.TeamB.TeamId] = c.TeamB.Name;

        var capA = c.TeamA == null ? (int?)null : Convert.ToInt32(c.TeamA.CaptainAccountId);
        var capB = c.TeamB == null ? (int?)null : Convert.ToInt32(c.TeamB.CaptainAccountId);
        var ids = rows.Select(r => Convert.ToInt32(r.SenderAccountId)).Append(c.Organizer).ToList();
        if (c.Referee != null) ids.Add(c.Referee.Value);
        if (capA != null) ids.Add(capA.Value);
        if (capB != null) ids.Add(capB.Value);
        var names = await NamesAsync(ids, ct);
        string Nm(int id) => names.TryGetValue(id, out var n) ? n : "#" + id;

        var parts = new List<ChatParticipant>();
        if (c.TeamA != null && capA != null) parts.Add(new(capA.Value, Nm(capA.Value), "CAPTAIN", c.TeamA.TeamId, c.TeamA.Name));
        if (c.TeamB != null && capB != null) parts.Add(new(capB.Value, Nm(capB.Value), "CAPTAIN", c.TeamB.TeamId, c.TeamB.Name));
        parts.Add(new(c.Organizer, Nm(c.Organizer), "ORGANIZER", null, null));
        if (c.Referee != null) parts.Add(new(c.Referee.Value, Nm(c.Referee.Value), "REFEREE", null, null));

        bool canSend = who.Value.Role != "ADMIN" && IsWritable(c);
        string? closed = who.Value.Role == "ADMIN" ? "READ_ONLY" : canSend ? null : "LOBBY_CLOSED";

        var h = new ChatHistory(matchId, c.Match.Status, c.TeamA?.Name ?? "—", c.TeamB?.Name ?? "—", who.Value.Role, who.Value.TeamId,
            canSend, closed, more, Now, parts, rows.Select(r => ToDto(r, names, teamNames)).ToList());
        return new(ChatError.None, h);
    }

    /// <summary>Chuẩn hoá nội dung: thống nhất xuống dòng, bỏ ký tự điều khiển, cắt khoảng trắng đầu/cuối.</summary>
    public static string Clean(string? raw)
    {
        if (string.IsNullOrEmpty(raw)) return "";
        var sb = new StringBuilder(raw.Length);
        var s = raw.Replace("\r\n", "\n").Replace('\r', '\n');
        foreach (var ch in s)
            if (ch == '\n' || ch == '\t' || !char.IsControl(ch)) sb.Append(ch);
        return sb.ToString().Trim();
    }

    public async Task<ChatResult<ChatMessageDto>> SendAsync(int matchId, int actorId, string? role, string? text, CancellationToken ct = default)
    {
        var c = await LoadAsync(matchId, ct);
        if (c == null) return new(ChatError.NotFound);
        var who = Resolve(c, actorId, role);
        if (who == null || who.Value.Role == "ADMIN") return new(ChatError.Forbidden);
        if (!IsWritable(c)) return new(ChatError.LobbyClosed);

        var body = Clean(text);
        if (body.Length == 0) return new(ChatError.EmptyMessage);
        if (body.Length > MaxLength) return new(ChatError.TooLong);

        var since = Now - BurstWindow;
        int recent = await _db.Set<MatchChatMessage>().AsNoTracking()
            .CountAsync(x => x.SenderAccountId == actorId && x.SentAt >= since, ct);
        if (recent >= BurstLimit) return new(ChatError.RateLimited);

        var row = new MatchChatMessage
        {
            MatchId = matchId,
            SenderAccountId = actorId,
            SenderRole = who.Value.Role,
            TeamId = who.Value.TeamId,
            Body = body,
            SentAt = Now
        };
        _db.Set<MatchChatMessage>().Add(row);
        await _db.SaveChangesAsync(ct);

        var names = await NamesAsync(new[] { actorId }, ct);
        var teamNames = new Dictionary<int, string>();
        if (c.TeamA != null) teamNames[c.TeamA.TeamId] = c.TeamA.Name;
        if (c.TeamB != null) teamNames[c.TeamB.TeamId] = c.TeamB.Name;
        return new(ChatError.None, ToDto(row, names, teamNames));
    }

    public async Task<ChatResult<IReadOnlyList<ChatMessageDto>>> ExportAsync(int matchId, int actorId, string? role, CancellationToken ct = default)
    {
        var c = await LoadAsync(matchId, ct);
        if (c == null) return new(ChatError.NotFound);
        var who = Resolve(c, actorId, role);
        // Đội trưởng cũng có thể xem lịch sử qua History; xuất file chỉ dành cho người xử lý tranh chấp.
        if (who == null || who.Value.Role == "CAPTAIN") return new(ChatError.Forbidden);

        var rows = await _db.Set<MatchChatMessage>().AsNoTracking().Where(x => x.MatchId == matchId)
            .OrderBy(x => x.MatchChatMessageId).ToListAsync(ct);
        var names = await NamesAsync(rows.Select(r => Convert.ToInt32(r.SenderAccountId)), ct);
        var teamNames = new Dictionary<int, string>();
        if (c.TeamA != null) teamNames[c.TeamA.TeamId] = c.TeamA.Name;
        if (c.TeamB != null) teamNames[c.TeamB.TeamId] = c.TeamB.Name;
        IReadOnlyList<ChatMessageDto> list = rows.Select(r => ToDto(r, names, teamNames)).ToList();
        return new(ChatError.None, list);
    }
}