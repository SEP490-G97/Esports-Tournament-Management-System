
using ETOMS_API.DTOs;
using ETOMS_API.Models;
using ETOMS_API.Repositories;
using ETOMS_API.Services.Interface;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ETOMS_API.Services.Impl;

public class ManualCheckInService : IManualCheckInService
{
    public const int MinReason = 10;
    public const int MaxReason = 500;   // Match.OverrideReason là varchar(500)

    private readonly EtomsDbContext _db;
    private readonly ILogger<ManualCheckInService> _log;
    private readonly IReadOnlyList<IMatchResultListener> _listeners;
    private readonly INotificationHub? _hub;
    private readonly TimeProvider _clock;

    public ManualCheckInService(EtomsDbContext db, ILogger<ManualCheckInService> log,
        IEnumerable<IMatchResultListener>? listeners = null, INotificationHub? hub = null, TimeProvider? clock = null)
    {
        _db = db;
        _log = log;
        _listeners = (listeners ?? Array.Empty<IMatchResultListener>()).ToList();
        _hub = hub;
        _clock = clock ?? TimeProvider.System;
    }

    public async Task<ManualCheckInResult> ApplyAsync(int matchId, int actorId, string? actorRole,
        ManualCheckInRequest request, CancellationToken ct = default)
    {
        var reason = (request?.Reason ?? "").Trim();
        if (reason.Length < MinReason || reason.Length > MaxReason) return new(ManualCheckInError.InvalidReason);
        var requested = (request?.TeamIds ?? new List<int>()).Distinct().ToList();
        if (requested.Count < 1 || requested.Count > 2) return new(ManualCheckInError.InvalidTeams);

        var m = await _db.Set<Match>().FirstOrDefaultAsync(x => x.MatchId == matchId, ct);
        if (m == null) return new(ManualCheckInError.NotFound);
        var t = await _db.Set<Tournament>().AsNoTracking().FirstOrDefaultAsync(x => x.TournamentId == m.TournamentId, ct);
        if (t == null) return new(ManualCheckInError.NotFound);

        bool isAdmin = actorRole == "ADMIN";
        if (!isAdmin && t.OrganizerAccountId != actorId) return new(ManualCheckInError.Forbidden);
        if (t.Status == "CANCELLED" || t.Status == "COMPLETED") return new(ManualCheckInError.TournamentClosed);

        if (m.TeamAid == null || m.TeamBid == null) return new(ManualCheckInError.InvalidStatus);
        int teamA = Convert.ToInt32(m.TeamAid), teamB = Convert.ToInt32(m.TeamBid);
        if (requested.Any(x => x != teamA && x != teamB)) return new(ManualCheckInError.InvalidTeams);

        var flag = await NoShowFlags.ReadAsync(_db, matchId, m.Status, ct);
        bool aIn = m.TeamAcheckedInAt != null, bIn = m.TeamBcheckedInAt != null;

        // Điều kiện theo trạng thái trận
        if (flag.Kind == NoShowKind.None)
        {
            if (m.Status != "READY") return new(ManualCheckInError.InvalidStatus);
            if (requested.Any(x => (x == teamA && aIn) || (x == teamB && bIn))) return new(ManualCheckInError.AlreadyCheckedIn);
        }
        else if (requested.Any(x => !flag.TeamIds.Contains(x)))
        {
            return new(ManualCheckInError.TeamNotAbsent);   // chỉ đội bị gắn cờ mới được gỡ
        }

        var now = _clock.GetUtcNow().UtcDateTime;
        var before = new
        {
            status = m.Status,
            winnerTeamId = m.WinnerTeamId,
            scoreA = m.ScoreA,
            scoreB = m.ScoreB,
            teamACheckedInAt = m.TeamAcheckedInAt,
            teamBCheckedInAt = m.TeamBcheckedInAt,
            checkInMode = m.CheckInMode
        };

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        if (flag.Kind == NoShowKind.Single)
        {
            // Thắng trắng do No-show: hoàn tác việc đưa đội đi tiếp TRƯỚC, rồi mới gỡ kết quả của chính trận này.
            try { foreach (var l in _listeners) await l.OnResultRevertAsync(matchId, ct); }
            catch (ResultRollbackBlockedException ex)
            {
                await tx.RollbackAsync(ct);
                _db.ChangeTracker.Clear();
                return new(ManualCheckInError.RollbackBlocked, null, ex.Message);
            }
            m.WinnerTeamId = null; m.ScoreA = null; m.ScoreB = null; m.ResultSource = null; m.VerifiedAt = null;
            m.Status = "READY";
        }
        else if (flag.Kind == NoShowKind.Double)
        {
            m.Status = "READY";
            var dispute = await _db.Set<Dispute>()
                .Where(d => d.MatchId == matchId && d.OpenReason == "REPORT_TIMEOUT" && (d.Status == "OPEN" || d.Status == "UNDER_REVIEW"))
                .OrderByDescending(d => d.DisputeId).FirstOrDefaultAsync(ct);
            if (dispute != null) dispute.Status = "CLOSED";
        }

        if (requested.Contains(teamA)) m.TeamAcheckedInAt = now;
        if (requested.Contains(teamB)) m.TeamBcheckedInAt = now;
        m.CheckInMode = "MANUAL_OVERRIDE";
        m.OverrideReason = reason;

        var absent = new List<int>();
        if (m.TeamAcheckedInAt == null) absent.Add(teamA);
        if (m.TeamBcheckedInAt == null) absent.Add(teamB);

        _db.Set<AuditLog>().Add(new AuditLog
        {
            ActorAccountId = actorId,
            TournamentId = t.TournamentId,
            ActionType = "MANUAL_CHECKIN",
            ObjectType = "MATCH",
            ObjectId = matchId,
            BeforeValue = AdminAudit.ToJson(before),
            AfterValue = AdminAudit.ToJson(new
            {
                status = m.Status,
                checkedInTeamIds = requested,
                noShowFlagRemoved = flag.Kind != NoShowKind.None,
                noShowKind = flag.Kind.ToString(),
                stillAbsentTeamIds = absent,
                teamACheckedInAt = m.TeamAcheckedInAt,
                teamBCheckedInAt = m.TeamBcheckedInAt
            }),
            Reason = AdminAudit.Truncate(reason, 1000),
            CreatedAt = now
        });

        // Thông báo cho đội trưởng hai đội
        var teams = await _db.Set<Team>().AsNoTracking().Where(x => x.TeamId == teamA || x.TeamId == teamB).ToListAsync(ct);
        string Name(int id) => teams.FirstOrDefault(x => x.TeamId == id)?.Name ?? "Đội";
        var checkedNames = string.Join(", ", requested.Select(Name));
        string body = $"Ban tổ chức đã check-in thủ công cho {checkedNames}" +
                      (flag.Kind != NoShowKind.None ? " và gỡ cờ No-show; trận được tiếp tục." : ".") + $" Lý do: {reason}";
        var rows = teams.Select(x => Convert.ToInt32(x.CaptainAccountId)).Where(a => a > 0).Distinct()
            .Select(a => new Notification
            {
                AccountId = a,
                NotificationType = "CHECKIN_REMINDER",
                Title = "Check-in thủ công bởi BTC",
                Body = AdminAudit.Truncate(body, 1000)!,
                LinkUrl = "/match.html?id=" + matchId,
                Channel = "IN_APP",
                IsRead = false,
                SentAt = now
            }).ToList();
        _db.Set<Notification>().AddRange(rows);

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        if (_hub != null)
        {
            try
            {
                foreach (var n in rows)
                    _hub.Publish(Convert.ToInt32(n.AccountId), new UserNotificationItem(n.NotificationId, n.NotificationType, n.Title, n.Body, n.LinkUrl, false, now));
            }
            catch (Exception ex) { _log.LogWarning(ex, "Đẩy thông báo real-time check-in thủ công thất bại (đã lưu DB)"); }
        }

        var checkedIn = new List<int>();
        if (m.TeamAcheckedInAt != null) checkedIn.Add(teamA);
        if (m.TeamBcheckedInAt != null) checkedIn.Add(teamB);
        return new(ManualCheckInError.None, new ManualCheckInResponse(matchId, m.Status, checkedIn, flag.Kind != NoShowKind.None, absent));
    }
}