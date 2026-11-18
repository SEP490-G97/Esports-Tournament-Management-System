
using ETOMS_API.DTOs;
using ETOMS_API.Models;
using ETOMS_API.Repositories;
using ETOMS_API.Services.Interface;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ETOMS_API.Services.Impl;

public class SubstitutionService : ISubstitutionService
{
    public const int MaxReason = 300;
    public const int MinRejectNote = 5;

    private readonly EtomsDbContext _db;
    private readonly ILogger<SubstitutionService> _log;
    private readonly INotificationHub? _hub;
    private readonly TimeProvider _clock;

    public SubstitutionService(EtomsDbContext db, ILogger<SubstitutionService> log, INotificationHub? hub = null, TimeProvider? clock = null)
    {
        _db = db;
        _log = log;
        _hub = hub;
        _clock = clock ?? TimeProvider.System;
    }

    private DateTime Now => _clock.GetUtcNow().UtcDateTime;

    // ------------------------------------------------------------------ ngữ cảnh chung

    private sealed class Ctx
    {
        public Match Match = null!;
        public Tournament Tournament = null!;
        public Team? TeamA, TeamB;
        public int? Referee;
        public string Decider => Referee != null ? "REFEREE" : "ORGANIZER";
    }

    private async Task<Ctx?> LoadAsync(int matchId, CancellationToken ct)
    {
        var m = await _db.Set<Match>().FirstOrDefaultAsync(x => x.MatchId == matchId, ct);
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
            Referee = m.RefereeAccountId == null ? null : Convert.ToInt32(m.RefereeAccountId)
        };
    }

    private static bool IsCaptain(Team? team, int actor) => team != null && Convert.ToInt32(team.CaptainAccountId) == actor;

    private bool CanDecide(Ctx c, int actor, string? role) =>
        role == "ADMIN"
        || (c.Referee != null ? c.Referee == actor : Convert.ToInt32(c.Tournament.OrganizerAccountId) == actor);

    /// <summary>Đội hình hiệu lực = đăng ký (lineup) áp dụng các yêu cầu đã duyệt của trận.</summary>
    private async Task<(List<int> Starters, List<int> Subs, bool HasRegistration)> LineupAsync(int tournamentId, int teamId, List<MatchSubstitution> teamSubs, CancellationToken ct)
    {
        var reg = await _db.Set<Registration>().AsNoTracking()
            .Where(r => r.TournamentId == tournamentId && r.TeamId == teamId && r.Status == "APPROVED")
            .Select(r => r.RegistrationId).FirstOrDefaultAsync(ct);
        if (reg == 0) return (new List<int>(), new List<int>(), false);

        var rows = await _db.Set<RegistrationLineup>().AsNoTracking().Where(l => l.RegistrationId == reg).ToListAsync(ct);
        var starters = rows.Where(l => l.Slot == "STARTER").Select(l => Convert.ToInt32(l.AccountId)).ToList();
        var subs = rows.Where(l => l.Slot == "SUBSTITUTE").Select(l => Convert.ToInt32(l.AccountId)).ToList();

        foreach (var s in teamSubs.Where(x => x.Status == "APPROVED").OrderBy(x => x.DecidedAt).ThenBy(x => x.MatchSubstitutionId))
        {
            int o = Convert.ToInt32(s.OutAccountId), i = Convert.ToInt32(s.InAccountId);
            if (starters.Remove(o) && subs.Remove(i)) { starters.Add(i); subs.Add(o); }
        }
        return (starters, subs, true);
    }

    private async Task<Dictionary<int, SubstitutionPlayer>> PlayersAsync(IEnumerable<int> ids, CancellationToken ct)
    {
        var list = ids.Distinct().ToList();
        var accounts = list.Count == 0 ? new List<Account>() : await _db.Set<Account>().AsNoTracking().Where(a => list.Contains(a.AccountId)).ToListAsync(ct);
        return list.ToDictionary(id => id, id =>
        {
            var a = accounts.FirstOrDefault(x => x.AccountId == id);
            return new SubstitutionPlayer(id, a?.FullName ?? ("#" + id), a?.AvatarUrl);
        });
    }

    // ------------------------------------------------------------------ xem

    public async Task<SubstitutionResult<SubstitutionBoard>> GetBoardAsync(int matchId, int actorId, string? actorRole, CancellationToken ct = default)
    {
        var c = await LoadAsync(matchId, ct);
        if (c == null) return new(SubstitutionError.NotFound);

        bool isCapA = IsCaptain(c.TeamA, actorId), isCapB = IsCaptain(c.TeamB, actorId);
        bool member = isCapA || isCapB;
        if (!member && c.TeamA != null && c.TeamB != null)
        {
            var mine = await _db.Set<TeamMember>().AsNoTracking()
                .Where(x => x.AccountId == actorId && x.Status == "ACTIVE" && (x.TeamId == c.TeamA.TeamId || x.TeamId == c.TeamB.TeamId)).AnyAsync(ct);
            member = mine;
        }
        bool decider = CanDecide(c, actorId, actorRole);
        bool isReferee = c.Referee == actorId;
        bool isOrganizer = Convert.ToInt32(c.Tournament.OrganizerAccountId) == actorId;
        if (!member && !decider && !isReferee && !isOrganizer && actorRole != "ADMIN") return new(SubstitutionError.Forbidden);
        if (c.TeamA == null || c.TeamB == null) return new(SubstitutionError.MatchNotEditable);

        var subs = await _db.Set<MatchSubstitution>().AsNoTracking().Where(s => s.MatchId == matchId)
            .OrderBy(s => s.MatchSubstitutionId).ToListAsync(ct);
        var la = await LineupAsync(c.Tournament.TournamentId, c.TeamA.TeamId, subs.Where(s => s.TeamId == c.TeamA.TeamId).ToList(), ct);
        var lb = await LineupAsync(c.Tournament.TournamentId, c.TeamB.TeamId, subs.Where(s => s.TeamId == c.TeamB.TeamId).ToList(), ct);

        var deciderIds = subs.Where(s => s.DecidedByAccountId != null).Select(s => Convert.ToInt32(s.DecidedByAccountId));
        var all = la.Starters.Concat(la.Subs).Concat(lb.Starters).Concat(lb.Subs)
            .Concat(subs.SelectMany(s => new[] { Convert.ToInt32(s.OutAccountId), Convert.ToInt32(s.InAccountId) })).ToList();
        var players = await PlayersAsync(all, ct);
        var deciderNames = await PlayersAsync(deciderIds, ct);

        SubstitutionTeamLineup TeamView(Team team, (List<int> Starters, List<int> Subs, bool Has) l) => new(
            team.TeamId, team.Name, l.Starters.Select(i => players[i]).ToList(), l.Subs.Select(i => players[i]).ToList(),
            c.Tournament.MaxSubstitutes, subs.Count(s => s.TeamId == team.TeamId && (s.Status == "PENDING" || s.Status == "APPROVED")));

        var now = Now;
        bool open = WindowOpen(c.Match, now);
        var items = subs.Select(s => new SubstitutionItem(
            s.MatchSubstitutionId, s.TeamId, s.TeamId == c.TeamA.TeamId ? c.TeamA.Name : c.TeamB.Name,
            players[Convert.ToInt32(s.OutAccountId)], players[Convert.ToInt32(s.InAccountId)], s.Reason, s.Status, s.RequestedAt,
            s.DecidedByAccountId == null ? null : deciderNames[Convert.ToInt32(s.DecidedByAccountId)].Name, s.DecidedAt, s.DecisionNote)).ToList();

        return new(SubstitutionError.None, new SubstitutionBoard(
            matchId, c.Match.Status, c.Match.ScheduledAt, now, open, c.Decider,
            isCapA ? c.TeamA.TeamId : isCapB ? c.TeamB.TeamId : (int?)null,
            (isCapA || isCapB) && open && c.Match.Status == "READY",
            decider && c.Match.Status == "READY",
            TeamView(c.TeamA, la), TeamView(c.TeamB, lb), items));
    }

    /// <summary>Chỉ được gửi yêu cầu khi trận READY và còn trước giờ thi đấu.</summary>
    private static bool WindowOpen(Match m, DateTime now) => m.Status == "READY" && m.ScheduledAt != null && now < m.ScheduledAt.Value;

    // ------------------------------------------------------------------ MAT-05: gửi yêu cầu

    public async Task<SubstitutionResult<SubstitutionItem>> RequestAsync(int matchId, int actorId, SubstitutionRequestBody body, CancellationToken ct = default)
    {
        var reason = (body?.Reason ?? "").Trim();
        if (reason.Length > MaxReason) return new(SubstitutionError.InvalidReason);
        if (body == null || body.OutAccountId == body.InAccountId || body.OutAccountId <= 0 || body.InAccountId <= 0)
            return new(SubstitutionError.InvalidPlayers);

        var c = await LoadAsync(matchId, ct);
        if (c == null) return new(SubstitutionError.NotFound);
        var team = IsCaptain(c.TeamA, actorId) ? c.TeamA : IsCaptain(c.TeamB, actorId) ? c.TeamB : null;
        if (team == null) return new(SubstitutionError.NotCaptain);
        if (c.Tournament.Status == "CANCELLED" || c.Tournament.Status == "COMPLETED") return new(SubstitutionError.MatchNotEditable);
        if (c.Match.Status != "READY") return new(SubstitutionError.MatchNotEditable);
        if (!WindowOpen(c.Match, Now)) return new(SubstitutionError.WindowClosed);

        var subs = await _db.Set<MatchSubstitution>().Where(s => s.MatchId == matchId && s.TeamId == team.TeamId).ToListAsync(ct);
        var (starters, bench, hasReg) = await LineupAsync(c.Tournament.TournamentId, team.TeamId, subs, ct);
        if (!hasReg) return new(SubstitutionError.NoRegistration);
        if (!starters.Contains(body.OutAccountId) || !bench.Contains(body.InAccountId)) return new(SubstitutionError.InvalidPlayers);

        var pending = subs.Where(s => s.Status == "PENDING").ToList();
        if (pending.Any(s => Convert.ToInt32(s.OutAccountId) == body.OutAccountId || Convert.ToInt32(s.InAccountId) == body.OutAccountId
                          || Convert.ToInt32(s.OutAccountId) == body.InAccountId || Convert.ToInt32(s.InAccountId) == body.InAccountId))
            return new(SubstitutionError.PlayerBusy);

        int used = subs.Count(s => s.Status == "PENDING" || s.Status == "APPROVED");
        if (used >= c.Tournament.MaxSubstitutes) return new(SubstitutionError.QuotaExceeded);

        var now = Now;
        var row = new MatchSubstitution
        {
            MatchId = matchId,
            TeamId = team.TeamId,
            OutAccountId = body.OutAccountId,
            InAccountId = body.InAccountId,
            Reason = reason.Length == 0 ? null : reason,
            Status = "PENDING",
            RequestedByAccountId = actorId,
            RequestedAt = now
        };
        _db.Set<MatchSubstitution>().Add(row);

        // Báo người có quyền duyệt: trọng tài của trận; không có thì BTC.
        int deciderId = c.Referee ?? Convert.ToInt32(c.Tournament.OrganizerAccountId);
        var players = await PlayersAsync(new[] { body.OutAccountId, body.InAccountId }, ct);
        var notes = new List<Notification>();
        if (deciderId > 0)
            notes.Add(Note(deciderId, "Yêu cầu thay người", $"{team.Name} xin thay {players[body.OutAccountId].Name} bằng {players[body.InAccountId].Name} (trận #{matchId}).", matchId, now));
        await SaveAndPushAsync(notes, now, ct);

        return new(SubstitutionError.None, new SubstitutionItem(row.MatchSubstitutionId, team.TeamId, team.Name,
            players[body.OutAccountId], players[body.InAccountId], row.Reason, row.Status, now, null, null, null));
    }

    public async Task<SubstitutionResult<SubstitutionItem>> CancelAsync(int matchId, int substitutionId, int actorId, CancellationToken ct = default)
    {
        var c = await LoadAsync(matchId, ct);
        if (c == null) return new(SubstitutionError.NotFound);
        var s = await _db.Set<MatchSubstitution>().FirstOrDefaultAsync(x => x.MatchSubstitutionId == substitutionId && x.MatchId == matchId, ct);
        if (s == null) return new(SubstitutionError.NotFound);
        var team = s.TeamId == c.TeamA?.TeamId ? c.TeamA : s.TeamId == c.TeamB?.TeamId ? c.TeamB : null;
        if (!IsCaptain(team, actorId)) return new(SubstitutionError.NotCaptain);
        if (s.Status != "PENDING") return new(SubstitutionError.NotPending);

        s.Status = "CANCELLED";
        await _db.SaveChangesAsync(ct);
        return new(SubstitutionError.None, await ItemAsync(s, team!.Name, ct));
    }

    // ------------------------------------------------------------------ MAT-06: duyệt

    public async Task<SubstitutionResult<SubstitutionItem>> DecideAsync(int matchId, int substitutionId, int actorId, string? actorRole,
        SubstitutionDecisionBody body, CancellationToken ct = default)
    {
        var note = (body?.Note ?? "").Trim();
        if (note.Length > MaxReason) return new(SubstitutionError.InvalidReason);

        var c = await LoadAsync(matchId, ct);
        if (c == null) return new(SubstitutionError.NotFound);
        if (!CanDecide(c, actorId, actorRole)) return new(SubstitutionError.NotDecider);

        var s = await _db.Set<MatchSubstitution>().FirstOrDefaultAsync(x => x.MatchSubstitutionId == substitutionId && x.MatchId == matchId, ct);
        if (s == null) return new(SubstitutionError.NotFound);
        if (s.Status != "PENDING") return new(SubstitutionError.NotPending);
        if (c.Match.Status != "READY") return new(SubstitutionError.MatchNotEditable);   // trận đã bắt đầu / kết thúc
        if (!body!.Approve && note.Length < MinRejectNote) return new(SubstitutionError.NoteRequired);

        var team = s.TeamId == c.TeamA?.TeamId ? c.TeamA! : c.TeamB!;
        int outId = Convert.ToInt32(s.OutAccountId), inId = Convert.ToInt32(s.InAccountId);

        if (body.Approve)
        {
            // Kiểm tra lại: đội hình có thể đã đổi từ lúc gửi (vd người bị xóa khỏi đăng ký).
            var teamSubs = await _db.Set<MatchSubstitution>().Where(x => x.MatchId == matchId && x.TeamId == s.TeamId).ToListAsync(ct);
            var (starters, bench, hasReg) = await LineupAsync(c.Tournament.TournamentId, s.TeamId, teamSubs, ct);
            if (!hasReg) return new(SubstitutionError.NoRegistration);
            if (!starters.Contains(outId) || !bench.Contains(inId)) return new(SubstitutionError.InvalidPlayers);
        }

        var now = Now;
        s.Status = body.Approve ? "APPROVED" : "REJECTED";
        s.DecidedByAccountId = actorId;
        s.DecidedAt = now;
        s.DecisionNote = note.Length == 0 ? null : note;

        var players = await PlayersAsync(new[] { outId, inId }, ct);
        string what = $"thay {players[outId].Name} bằng {players[inId].Name}";
        _db.Set<AuditLog>().Add(new AuditLog
        {
            ActorAccountId = actorId,
            TournamentId = c.Tournament.TournamentId,
            ActionType = body.Approve ? "APPROVE" : "REJECT",
            ObjectType = "SUBSTITUTION",
            ObjectId = s.MatchSubstitutionId,
            BeforeValue = AdminAudit.ToJson(new { status = "PENDING", matchId, teamId = s.TeamId, outAccountId = outId, inAccountId = inId }),
            AfterValue = AdminAudit.ToJson(new { status = s.Status, decider = c.Decider }),
            Reason = AdminAudit.Truncate(note.Length == 0 ? $"Duyệt yêu cầu {what}" : note, 1000),
            CreatedAt = now
        });

        var notes = new List<Notification>();
        int requester = Convert.ToInt32(s.RequestedByAccountId);
        string verdict = body.Approve ? "được duyệt" : "bị từ chối";
        notes.Add(Note(requester, "Kết quả yêu cầu thay người", $"Yêu cầu {what} {verdict}." + (note.Length > 0 ? $" Ghi chú: {note}" : ""), matchId, now));
        if (body.Approve)
        {
            var other = team.TeamId == c.TeamA?.TeamId ? c.TeamB : c.TeamA;
            if (other != null && Convert.ToInt32(other.CaptainAccountId) > 0)
                notes.Add(Note(Convert.ToInt32(other.CaptainAccountId), "Đối thủ thay người", $"{team.Name} đã được duyệt {what} (trận #{matchId}).", matchId, now));
        }
        await SaveAndPushAsync(notes, now, ct);

        return new(SubstitutionError.None, await ItemAsync(s, team.Name, ct));
    }

    // ------------------------------------------------------------------ tiện ích

    private static Notification Note(int account, string title, string body, int matchId, DateTime now) => new()
    {
        AccountId = account,
        NotificationType = "SYSTEM",
        Title = title,
        Body = AdminAudit.Truncate(body, 1000)!,
        LinkUrl = "/substitution.html?id=" + matchId,
        Channel = "IN_APP",
        IsRead = false,
        SentAt = now
    };

    private async Task SaveAndPushAsync(List<Notification> notes, DateTime now, CancellationToken ct)
    {
        notes = notes.Where(n => Convert.ToInt32(n.AccountId) > 0).ToList();
        _db.Set<Notification>().AddRange(notes);
        await _db.SaveChangesAsync(ct);
        if (_hub == null) return;
        try
        {
            foreach (var n in notes)
                _hub.Publish(Convert.ToInt32(n.AccountId), new UserNotificationItem(n.NotificationId, n.NotificationType, n.Title, n.Body, n.LinkUrl, false, now));
        }
        catch (Exception ex) { _log.LogWarning(ex, "Đẩy thông báo real-time thay người thất bại (đã lưu DB)"); }
    }

    private async Task<SubstitutionItem> ItemAsync(MatchSubstitution s, string teamName, CancellationToken ct)
    {
        var ids = new List<int> { Convert.ToInt32(s.OutAccountId), Convert.ToInt32(s.InAccountId) };
        if (s.DecidedByAccountId != null) ids.Add(Convert.ToInt32(s.DecidedByAccountId));
        var p = await PlayersAsync(ids, ct);
        return new SubstitutionItem(s.MatchSubstitutionId, s.TeamId, teamName, p[Convert.ToInt32(s.OutAccountId)], p[Convert.ToInt32(s.InAccountId)],
            s.Reason, s.Status, s.RequestedAt, s.DecidedByAccountId == null ? null : p[Convert.ToInt32(s.DecidedByAccountId)].Name, s.DecidedAt, s.DecisionNote);
    }
}