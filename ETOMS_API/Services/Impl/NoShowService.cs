
using ETOMS_API.DTOs;
using ETOMS_API.Models;
using ETOMS_API.Repositories;
using ETOMS_API.Services.Interface;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ETOMS_API.Services.Impl;

public class NoShowService : INoShowService
{
    private const int BatchSize = 100;
    private const int MaxRoundsPerScan = 10;

    private readonly EtomsDbContext _db;
    private readonly ILogger<NoShowService> _log;
    private readonly IReadOnlyList<IMatchResultListener> _listeners;
    private readonly INotificationHub? _hub;
    private readonly TimeProvider _clock;

    public NoShowService(EtomsDbContext db, ILogger<NoShowService> log,
        IEnumerable<IMatchResultListener>? listeners = null, INotificationHub? hub = null, TimeProvider? clock = null)
    {
        _db = db;
        _log = log;
        _listeners = (listeners ?? Array.Empty<IMatchResultListener>()).ToList();
        _hub = hub;
        _clock = clock ?? TimeProvider.System;
    }

    private enum Outcome { Skipped, Walkover, DoubleNoShow }

    public async Task<NoShowScanResult> ScanAsync(DateTime? nowUtc = null, CancellationToken ct = default)
    {
        var now = nowUtc ?? _clock.GetUtcNow().UtcDateTime;
        int scanned = 0, walkovers = 0, doubles = 0;
        var failures = new List<int>();
        var seen = new HashSet<int>();

        for (int round = 0; round < MaxRoundsPerScan; round++)
        {
            _db.ChangeTracker.Clear();
            var ids = await _db.Set<Match>().AsNoTracking()
                .Where(m => m.Status == "READY" && m.ScheduledAt != null && m.ScheduledAt <= now
                            && m.TeamAid != null && m.TeamBid != null
                            && !(m.TeamAcheckedInAt != null && m.TeamBcheckedInAt != null))
                .OrderBy(m => m.ScheduledAt).Select(m => m.MatchId).Take(BatchSize).ToListAsync(ct);
            ids = ids.Where(seen.Add).ToList();
            if (ids.Count == 0) break;

            foreach (var id in ids)
            {
                ct.ThrowIfCancellationRequested();
                scanned++;
                Outcome outcome;
                try { outcome = await ProcessAsync(id, now, ct); }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    _log.LogError(ex, "Không xử lý được No-show cho trận {MatchId}", id);
                    _db.ChangeTracker.Clear();
                    continue;
                }

                if (outcome == Outcome.Walkover)
                {
                    walkovers++;
                    foreach (var l in _listeners)
                    {
                        try { await l.OnResultAsync(id, ct); }
                        catch (OperationCanceledException) { throw; }
                        catch (Exception ex)
                        {
                            // Kết quả đã lưu; bước sau (đưa đội đi tiếp) sẽ báo lỗi để người vận hành xử lý / chạy lại.
                            _log.LogError(ex, "Listener {Listener} lỗi sau khi xử thắng trắng trận {MatchId}", l.GetType().Name, id);
                            failures.Add(id);
                        }
                    }
                }
                else if (outcome == Outcome.DoubleNoShow) doubles++;
            }
        }
        return new NoShowScanResult(scanned, walkovers, doubles, failures.Distinct().ToList());
    }

    private async Task<Outcome> ProcessAsync(int matchId, DateTime now, CancellationToken ct)
    {
        _db.ChangeTracker.Clear();
        var m = await _db.Set<Match>().FirstOrDefaultAsync(x => x.MatchId == matchId, ct);
        // Đọc lại điều kiện: có thể đã đổi trạng thái / vừa check-in kể từ lúc quét.
        if (m == null || m.Status != "READY" || m.ScheduledAt == null || m.ScheduledAt > now
            || m.TeamAid == null || m.TeamBid == null) return Outcome.Skipped;
        bool aIn = m.TeamAcheckedInAt != null, bIn = m.TeamBcheckedInAt != null;
        if (aIn && bIn) return Outcome.Skipped;

        var t = await _db.Set<Tournament>().AsNoTracking().FirstOrDefaultAsync(x => x.TournamentId == m.TournamentId, ct);
        if (t == null || t.Status == "CANCELLED" || t.Status == "COMPLETED") return Outcome.Skipped;

        int teamA = Convert.ToInt32(m.TeamAid), teamB = Convert.ToInt32(m.TeamBid);
        var teams = await _db.Set<Team>().AsNoTracking().Where(x => x.TeamId == teamA || x.TeamId == teamB).ToListAsync(ct);
        var ta = teams.FirstOrDefault(x => x.TeamId == teamA);
        var tb = teams.FirstOrDefault(x => x.TeamId == teamB);
        string nameA = ta?.Name ?? "Đội A", nameB = tb?.Name ?? "Đội B";
        int capA = ta == null ? 0 : Convert.ToInt32(ta.CaptainAccountId);
        int capB = tb == null ? 0 : Convert.ToInt32(tb.CaptainAccountId);
        int org = Convert.ToInt32(t.OrganizerAccountId);
        string link = "/match.html?id=" + matchId;
        var notes = new List<(int Account, string Title, string Body)>();
        Outcome outcome;

        if (!aIn && !bIn)
        {
            outcome = Outcome.DoubleNoShow;
            m.Status = "DISPUTED";
            _db.Set<Dispute>().Add(new Dispute
            {
                MatchId = matchId,
                OpenReason = "REPORT_TIMEOUT",
                Status = "OPEN",
                AppealStatus = "NONE",
                AppealCount = 0
            });
            Audit(t.TournamentId, matchId, "READY", "DISPUTED", "DOUBLE_NO_SHOW",
                new { outcome = "DOUBLE_NO_SHOW", noShowTeamIds = new[] { teamA, teamB } },
                $"No-show: cả hai đội ({nameA}, {nameB}) không check-in trước giờ thi đấu; cần BTC quyết định.");
            notes.Add((capA, "Trận đấu bị đình chỉ", $"Cả hai đội đều không check-in trước giờ thi đấu. Ban tổ chức sẽ xem xét trận {nameA} vs {nameB}."));
            notes.Add((capB, "Trận đấu bị đình chỉ", $"Cả hai đội đều không check-in trước giờ thi đấu. Ban tổ chức sẽ xem xét trận {nameA} vs {nameB}."));
            notes.Add((org, "Trận có hai đội vắng mặt", $"Trận {nameA} vs {nameB}: cả hai đội không check-in. Cần quyết định xử lý."));
        }
        else
        {
            outcome = Outcome.Walkover;
            int winner = aIn ? teamA : teamB, loser = aIn ? teamB : teamA;
            string winName = aIn ? nameA : nameB, loseName = aIn ? nameB : nameA;
            int winCap = aIn ? capA : capB, loseCap = aIn ? capB : capA;
            int winScore = m.BestOf / 2 + 1;   // BO1 → 1-0, BO3 → 2-0, BO5 → 3-0

            m.Status = "WALKOVER";
            m.WinnerTeamId = winner;
            m.ResultSource = "WALKOVER";
            m.ScoreA = aIn ? winScore : 0;
            m.ScoreB = aIn ? 0 : winScore;
            m.VerifiedAt = now;
            Audit(t.TournamentId, matchId, "READY", "WALKOVER", "NO_SHOW",
                new { outcome = "WALKOVER", winnerTeamId = winner, noShowTeamId = loser, scoreA = m.ScoreA, scoreB = m.ScoreB },
                $"No-show: {loseName} không check-in trước giờ thi đấu → {winName} thắng trắng.");
            notes.Add((winCap, "Bạn thắng trắng", $"Đối thủ {loseName} không check-in kịp nên {winName} được xử thắng trắng."));
            notes.Add((loseCap, "Bị xử thua do vắng mặt", $"{loseName} không check-in trước giờ thi đấu nên bị xử thua trắng."));
            notes.Add((org, "Trận xử thắng trắng", $"Trận {nameA} vs {nameB}: {loseName} no-show, {winName} thắng trắng."));
        }

        var rows = new List<Notification>();
        foreach (var (acc, title, body) in notes.Where(n => n.Account > 0).DistinctBy(n => (n.Account, n.Title)))
            rows.Add(new Notification
            {
                AccountId = acc,
                NotificationType = "MATCH_RESULT",
                Title = title,
                Body = body,
                LinkUrl = link,
                Channel = "IN_APP",
                IsRead = false,
                SentAt = now
            });
        _db.Set<Notification>().AddRange(rows);
        await _db.SaveChangesAsync(ct);

        if (_hub != null)
        {
            try
            {
                foreach (var n in rows)
                    _hub.Publish(Convert.ToInt32(n.AccountId), new UserNotificationItem(n.NotificationId, n.NotificationType, n.Title, n.Body, n.LinkUrl, false, now));
            }
            catch (Exception ex) { _log.LogWarning(ex, "Đẩy thông báo real-time No-show thất bại (đã lưu DB)"); }
        }
        return outcome;
    }

    private void Audit(int tournamentId, int matchId, string before, string after, string reason, object detail, string note)
    {
        _db.Set<AuditLog>().Add(new AuditLog
        {
            ActorAccountId = null,   // hệ thống
            TournamentId = tournamentId,
            ActionType = "WALKOVER",
            ObjectType = "MATCH",
            ObjectId = matchId,
            BeforeValue = AdminAudit.ToJson(new { status = before }),
            AfterValue = AdminAudit.ToJson(new { status = after, reason, detail }),
            Reason = AdminAudit.Truncate(note, 1000),
            CreatedAt = DateTime.UtcNow
        });
    }
}