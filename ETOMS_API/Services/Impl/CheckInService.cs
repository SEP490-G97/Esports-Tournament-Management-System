using ETOMS_API.DTOs;
using ETOMS_API.Models;
using ETOMS_API.Repositories;
using ETOMS_API.Services.Interface;
using ETOMS_API.Utils;
using Microsoft.EntityFrameworkCore;

namespace ETOMS_API.Services.Impl;

public class CheckInService : ICheckInService
{
    private readonly EtomsDbContext _db;
    private readonly TimeProvider _clock;

    public CheckInService(EtomsDbContext db, TimeProvider? clock = null)
    {
        _db = db;
        _clock = clock ?? TimeProvider.System;
    }

    private DateTime Now => _clock.GetUtcNow().UtcDateTime;

    public async Task<CheckInResult> GetStateAsync(int matchId, int actorId, CancellationToken ct = default)
    {
        var (m, side, error) = await LoadAsync(matchId, actorId, tracking: false, ct);
        return error != CheckInError.None ? new(error) : new(CheckInError.None, await BuildAsync(m!, side, ct));
    }

    public async Task<CheckInResult> CheckInAsync(int matchId, int actorId, CancellationToken ct = default)
    {
        var (m, side, error) = await LoadAsync(matchId, actorId, tracking: true, ct);
        if (error != CheckInError.None) return new(error);

        var now = Now;
        if (m!.Status != "READY" || m.TeamAid == null || m.TeamBid == null) return new(CheckInError.NotReady);
        switch (CheckInRules.PhaseOf(m.ScheduledAt, now))
        {
            case CheckInPhase.NotScheduled: return new(CheckInError.NotScheduled);
            case CheckInPhase.NotYet: return new(CheckInError.NotYetOpen);
            case CheckInPhase.Closed: return new(CheckInError.Closed);
        }
        if ((side == 'A' ? m.TeamAcheckedInAt : m.TeamBcheckedInAt) != null) return new(CheckInError.AlreadyCheckedIn);

        if (side == 'A') m.TeamAcheckedInAt = now; else m.TeamBcheckedInAt = now;
        await _db.SaveChangesAsync(ct);
        return new(CheckInError.None, await BuildAsync(m, side, ct));
    }

    private async Task<(Match? M, char Side, CheckInError Error)> LoadAsync(int matchId, int actorId, bool tracking, CancellationToken ct)
    {
        IQueryable<Match> q = _db.Set<Match>();
        if (!tracking) q = q.AsNoTracking();
        var m = await q.FirstOrDefaultAsync(x => x.MatchId == matchId, ct);
        if (m == null) return (null, ' ', CheckInError.NotFound);

        var ids = new[] { m.TeamAid, m.TeamBid }.Where(x => x != null).Select(x => Convert.ToInt32(x)).ToList();
        var caps = ids.Count == 0
            ? new List<(int TeamId, int Captain)>()
            : (await _db.Set<Team>().AsNoTracking().Where(t => ids.Contains(t.TeamId))
                .Select(t => new { t.TeamId, t.CaptainAccountId }).ToListAsync(ct))
                .Select(t => (TeamId: t.TeamId, Captain: Convert.ToInt32(t.CaptainAccountId))).ToList();

        if (m.TeamAid != null && caps.Any(c => c.TeamId == m.TeamAid && c.Captain == actorId)) return (m, 'A', CheckInError.None);
        if (m.TeamBid != null && caps.Any(c => c.TeamId == m.TeamBid && c.Captain == actorId)) return (m, 'B', CheckInError.None);
        return (null, ' ', CheckInError.Forbidden);
    }

    private async Task<CheckInState> BuildAsync(Match m, char side, CancellationToken ct)
    {
        var now = Now;
        var phase = CheckInRules.PhaseOf(m.ScheduledAt, now);
        var t = await _db.Set<Tournament>().AsNoTracking().FirstAsync(x => x.TournamentId == m.TournamentId, ct);
        var ids = new[] { m.TeamAid, m.TeamBid }.Where(x => x != null).Select(x => Convert.ToInt32(x)).ToList();
        var teams = await _db.Set<Team>().AsNoTracking().Where(x => ids.Contains(x.TeamId)).ToListAsync(ct);

        CheckInTeamView? View(int? id, DateTime? at)
        {
            var team = id == null ? null : teams.FirstOrDefault(x => x.TeamId == id);
            return team == null ? null : new CheckInTeamView(team.TeamId, team.Name, team.Tag, team.LogoUrl, at != null, at);
        }
        var a = View(m.TeamAid, m.TeamAcheckedInAt);
        var b = View(m.TeamBid, m.TeamBcheckedInAt);
        var mine = side == 'A' ? a! : b!;
        var opp = side == 'A' ? b : a;

        string? reason = null;
        if (m.Status != "READY" || opp == null) reason = "NOT_READY";
        else if (mine.CheckedIn) reason = "ALREADY_CHECKED_IN";
        else if (phase == CheckInPhase.NotScheduled) reason = "NOT_SCHEDULED";
        else if (phase == CheckInPhase.NotYet) reason = "NOT_YET";
        else if (phase == CheckInPhase.Closed) reason = "CLOSED";

        return new CheckInState(m.MatchId, t.Name, m.Status, m.ScheduledAt, CheckInRules.OpensAt(m.ScheduledAt), m.ScheduledAt,
            now, phase switch
            {
                CheckInPhase.NotScheduled => "NOT_SCHEDULED",
                CheckInPhase.NotYet => "NOT_YET",
                CheckInPhase.Open => "OPEN",
                _ => "CLOSED"
            }, mine, opp, reason == null, reason);
    }
}