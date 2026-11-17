
using ETOMS_API.DTOs;
using ETOMS_API.Models;
using ETOMS_API.Repositories;
using ETOMS_API.Services.Interface;
using ETOMS_API.Utils;
using Microsoft.EntityFrameworkCore;

namespace ETOMS_API.Services.Impl;

public class MatchDetailService : IMatchDetailService
{
    private readonly EtomsDbContext _db;

    public MatchDetailService(EtomsDbContext db) => _db = db;

    private static bool Finished(string? s) => s == "VERIFIED" || s == "WALKOVER";

    public async Task<MatchDetailResult> GetAsync(int matchId, MatchViewer viewer, CancellationToken ct = default)
    {
        var m = await _db.Set<Match>().AsNoTracking().FirstOrDefaultAsync(x => x.MatchId == matchId, ct);
        if (m == null) return new(MatchDetailError.NotFound);
        var t = await _db.Set<Tournament>().AsNoTracking().FirstOrDefaultAsync(x => x.TournamentId == m.TournamentId, ct);
        if (t == null) return new(MatchDetailError.NotFound);

        int? me = viewer.AccountId;
        bool isAdmin = me != null && viewer.Role == "ADMIN";
        bool isOrganizer = me != null && (isAdmin || t.OrganizerAccountId == me);
        // Giải nháp chỉ BTC / admin xem được; khách coi như không tồn tại.
        if (t.Status == "DRAFT" && !isOrganizer) return new(MatchDetailError.NotFound);

        var teamIds = new[] { m.TeamAid, m.TeamBid }.Where(x => x != null).Select(x => Convert.ToInt32(x)).ToList();
        var teams = teamIds.Count == 0
            ? new List<Team>()
            : await _db.Set<Team>().AsNoTracking().Where(x => teamIds.Contains(x.TeamId)).ToListAsync(ct);
        Team? ta = teams.FirstOrDefault(x => x.TeamId == m.TeamAid);
        Team? tb = teams.FirstOrDefault(x => x.TeamId == m.TeamBid);

        // Quan hệ của người xem với trận.
        bool isReferee = me != null && m.RefereeAccountId == me;
        bool capA = me != null && ta != null && ta.CaptainAccountId == me;
        bool capB = me != null && tb != null && tb.CaptainAccountId == me;
        bool memberA = capA, memberB = capB;
        if (me != null && teamIds.Count > 0 && !(capA && capB))
        {
            var mine = await _db.Set<TeamMember>().AsNoTracking()
                .Where(x => x.AccountId == me && x.Status == "ACTIVE" && teamIds.Contains(x.TeamId))
                .Select(x => x.TeamId).ToListAsync(ct);
            memberA |= ta != null && mine.Contains(ta.TeamId);
            memberB |= tb != null && mine.Contains(tb.TeamId);
        }
        bool isParticipant = memberA || memberB;
        bool privileged = isParticipant || isReferee || isOrganizer;

        string relation = isAdmin ? "ADMIN" : isOrganizer ? "ORGANIZER" : isReferee ? "REFEREE"
            : memberA ? "TEAM_A" : memberB ? "TEAM_B" : "NONE";
        string role = me == null ? "GUEST" : (viewer.Role ?? "PLAYER");

        // Vòng đấu.
        var node = await _db.Set<BracketNode>().AsNoTracking()
            .FirstOrDefaultAsync(n => n.BracketNodeId == m.BracketNodeId, ct);
        var round = await BuildRoundAsync(node, m.TournamentId, ct);

        var games = await _db.Set<MatchGame>().AsNoTracking().Where(g => g.MatchId == matchId)
            .OrderBy(g => g.GameNo).ToListAsync(ct);

        MatchRefereeView? referee = null;
        if (m.RefereeAccountId != null)
        {
            var r = await _db.Set<Account>().AsNoTracking().FirstOrDefaultAsync(a => a.AccountId == m.RefereeAccountId, ct);
            if (r != null) referee = new MatchRefereeView(r.AccountId, r.FullName, r.AvatarUrl);
        }

        var reports = await _db.Set<MatchReport>().AsNoTracking().Where(r => r.MatchId == matchId).ToListAsync(ct);
        var disputes = await _db.Set<Dispute>().AsNoTracking().Where(d => d.MatchId == matchId)
            .OrderByDescending(d => d.DisputeId).ToListAsync(ct);

        // Minh chứng: khách chỉ thấy khi trận đã chốt kết quả.
        bool evidenceVisible = privileged || Finished(m.Status);
        var evidence = new List<MatchEvidenceView>();
        if (evidenceVisible)
        {
            var reportIds = reports.Select(r => r.MatchReportId).ToList();
            var disputeIds = disputes.Select(d => d.DisputeId).ToList();
            var atts = await _db.Set<Attachment>().AsNoTracking()
                .Where(a => (a.OwnerType == "MATCH_REPORT" && reportIds.Contains(a.OwnerId))
                         || (a.OwnerType == "DISPUTE" && disputeIds.Contains(a.OwnerId)))
                .OrderBy(a => a.AttachmentId).ToListAsync(ct);
            foreach (var a in atts)
            {
                int? teamId = a.OwnerType == "MATCH_REPORT"
                    ? reports.FirstOrDefault(r => r.MatchReportId == a.OwnerId)?.TeamId : null;
                evidence.Add(new MatchEvidenceView(a.AttachmentId, a.OwnerType, a.FileType, a.FileUrl, a.SizeBytes,
                    teamId, a.UploadedAt));
            }
        }

        // Mã phòng: chỉ người liên quan và chỉ khi trận sắp / đang diễn ra.
        MatchRoomView? room = null;
        if (privileged && (m.Status == "READY" || m.Status == "LIVE") && (m.RoomCode != null || m.RoomPassword != null))
            room = new MatchRoomView(m.RoomCode, m.RoomPassword);

        var openDispute = disputes.FirstOrDefault(d => d.Status == "OPEN" || d.Status == "UNDER_REVIEW");
        var shownDispute = openDispute ?? disputes.FirstOrDefault();

        // Nút thao tác theo role.
        var actions = new List<string>();
        var now = DateTime.UtcNow;
        bool captain = capA || capB;
        if (captain)
        {
            bool checkedIn = capA ? m.TeamAcheckedInAt != null : m.TeamBcheckedInAt != null;
            if (m.Status == "READY" && m.TeamAid != null && m.TeamBid != null && !checkedIn
                && CheckInRules.PhaseOf(m.ScheduledAt, now) == CheckInPhase.Open)
                actions.Add(MatchActions.CheckIn);
            int myTeam = capA ? ta!.TeamId : tb!.TeamId;
            if ((m.Status == "LIVE" || m.Status == "WAITING_REPORT") && !reports.Any(r => r.TeamId == myTeam))
                actions.Add(MatchActions.SubmitReport);
            if ((m.Status == "WAITING_REPORT" || m.Status == "VERIFIED") && openDispute == null)
                actions.Add(MatchActions.OpenDispute);
        }
        if (isReferee)
        {
            if (m.Status == "READY") actions.Add(MatchActions.RefereeStart);
            if (m.Status == "LIVE" || m.Status == "WAITING_REPORT" || m.Status == "DISPUTED")
                actions.Add(MatchActions.RefereeScore);
        }
        if (isOrganizer && m.Status != "CANCELLED") actions.Add(MatchActions.Manage);

        // Thay người (MAT-05/06): đội trưởng gửi yêu cầu trước giờ đấu; trọng tài (hoặc BTC nếu không có trọng tài) duyệt.
        if (m.Status == "READY" && m.TeamAid != null && m.TeamBid != null)
        {
            if (captain && m.ScheduledAt != null && now < m.ScheduledAt.Value) actions.Add(MatchActions.Substitution);
            bool canDecide = isAdmin || (m.RefereeAccountId != null ? isReferee : (me != null && t.OrganizerAccountId == me));
            if (canDecide && await _db.Set<MatchSubstitution>().AsNoTracking().AnyAsync(x => x.MatchId == matchId && x.Status == "PENDING", ct))
                actions.Add(MatchActions.ReviewSubstitution);
        }

        // BTC / admin: cờ No-show còn hiệu lực + khả năng check-in thủ công (MAT-04). Chỉ người có quyền mới thấy thông tin này.
        MatchNoShowView? noShow = null;
        if (isOrganizer && t.Status != "CANCELLED" && t.Status != "COMPLETED" && m.TeamAid != null && m.TeamBid != null)
        {
            var flag = await NoShowFlags.ReadAsync(_db, matchId, m.Status, ct);
            if (flag.Kind != NoShowKind.None)
            {
                noShow = new MatchNoShowView(flag.Kind.ToString().ToUpperInvariant(), flag.TeamIds);
                actions.Add(MatchActions.ManualCheckIn);
            }
            else if (m.Status == "READY" && (m.TeamAcheckedInAt == null || m.TeamBcheckedInAt == null))
                actions.Add(MatchActions.ManualCheckIn);
        }

        MatchTeamView View(Team? team, int? score, DateTime? checkedAt) => new(
            team?.TeamId, team?.Name, team?.Tag, team?.LogoUrl, score, checkedAt != null,
            team != null && m.WinnerTeamId != null && m.WinnerTeamId == team.TeamId);

        return new(MatchDetailError.None, new MatchDetailResponse(
            m.MatchId,
            new MatchTournamentView(t.TournamentId, t.Name, t.Format, t.Status, t.StreamUrl),
            round,
            m.Status, m.BestOf, m.ScheduledAt, CheckInRules.OpensAt(m.ScheduledAt), m.ReportDeadlineAt, m.VerifiedAt,
            View(ta, m.ScoreA, m.TeamAcheckedInAt), View(tb, m.ScoreB, m.TeamBcheckedInAt),
            m.ScoreA, m.ScoreB, m.WinnerTeamId, m.ResultSource,
            referee,
            games.Select(g => new MatchGameView(g.GameNo, g.ScoreA, g.ScoreB, g.WinnerTeamId)).ToList(),
            evidenceVisible, evidence, room,
            shownDispute == null ? null : new MatchDisputeView(shownDispute.DisputeId, shownDispute.Status, shownDispute.OpenReason),
            new MatchViewerView(role, relation), actions, noShow));
    }

    /// <summary>Đặt tên vòng: Double Elimination (UB/LB/GF), vòng bảng, hoặc loại trực tiếp (Tứ kết/Bán kết/Chung kết).</summary>
    private async Task<MatchRoundView> BuildRoundAsync(BracketNode? node, int tournamentId, CancellationToken ct)
    {
        if (node == null) return new MatchRoundView("Trận đấu", null, null, null, null);
        string? gl = node.GroupLabel;
        int? rn = node.RoundNo;
        int? pos = node.PositionInRound;
        if (rn == null) return new MatchRoundView("Trận đấu", node.StageType, gl, null, pos);

        if (node.StageType == "GROUP")
            return new MatchRoundView($"Bảng {gl} · Lượt {rn}", "GROUP", gl, rn, pos);

        // Số vòng tối đa của cùng nhóm nút (cùng giai đoạn và cùng nhánh) để biết đâu là chung kết.
        int stage = node.StageNo;
        int? maxRoundN = await _db.Set<BracketNode>().AsNoTracking()
            .Where(n => n.TournamentId == tournamentId && n.StageNo == stage && n.GroupLabel == gl)
            .MaxAsync(n => (int?)n.RoundNo, ct);
        int maxRound = maxRoundN ?? Convert.ToInt32(rn);
        int fromEnd = maxRound - Convert.ToInt32(rn);

        string label;
        switch (gl)
        {
            case "GF":
                label = rn == 2 ? "Chung kết tổng · Bracket Reset" : "Chung kết tổng";
                break;
            case "UB":
                label = fromEnd == 0 ? "Chung kết nhánh thắng" : $"Nhánh thắng · Vòng {rn}";
                break;
            case "LB":
                label = fromEnd == 0 ? "Chung kết nhánh thua" : $"Nhánh thua · Vòng {rn}";
                break;
            default:
                label = fromEnd == 0 ? "Chung kết" : fromEnd == 1 ? "Bán kết" : fromEnd == 2 ? "Tứ kết" : $"Vòng {rn}";
                break;
        }
        return new MatchRoundView(label, node.StageType, gl, rn, pos);
    }
}