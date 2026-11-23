using System.Collections.Concurrent;
using ETOMS_API.Utils;
using ETOMS_API.DTOs.Result;
using ETOMS_API.Repositories;
using ETOMS_API.Repositories.Interface;
using ETOMS_API.Services.Interface;
using ETOMS_API.Models;
using Microsoft.EntityFrameworkCore;

namespace ETOMS_API.Services.Impl;

// Khóa trong-tiến-trình theo MatchID — chống race khi 2 đội trưởng nộp báo cáo cùng lúc (RES-02).
// Chỉ chạy 1 instance nên khóa bộ nhớ là đủ; nhiều instance thì phải đổi sang khóa tầng DB.
internal static class MatchReconcileLock
{
    private static readonly ConcurrentDictionary<int, SemaphoreSlim> Locks = new();
    public static SemaphoreSlim Get(int matchId) => Locks.GetOrAdd(matchId, _ => new SemaphoreSlim(1, 1));
}

public class ResultService : IResultService
{
    private readonly IUnitOfWork _uow;
    private readonly IServiceProvider _services;

    // IServiceProvider: lấy ITournamentLifecycleService lúc cần (UC-33 A1) mà không tạo vòng phụ thuộc khi dựng service.
    public ResultService(IUnitOfWork uow, IServiceProvider services)
    {
        _uow = uow;
        _services = services;
    }

    private static readonly string[] OpenMatchStatuses = { "PENDING", "READY", "LIVE", "WAITING_REPORT" };

    // ===== RES-01 (UC-27) =====

    public async Task<ReportSubmissionPageDto?> GetSubmissionPageAsync(int matchId, int captainAccountId)
    {
        var match = await _uow.Repository<Match>().Query()
            .Include(m => m.TeamA).Include(m => m.TeamB).Include(m => m.Tournament)
            .FirstOrDefaultAsync(m => m.MatchId == matchId);
        if (match is null) return null;

        var myTeamId = await ResolveCaptainTeamIdAsync(match, captainAccountId);
        if (myTeamId is null) return null;

        var opponentTeamId = myTeamId == match.TeamAid ? match.TeamBid : match.TeamAid;
        var alreadyReported = await _uow.Repository<MatchReport>()
            .AnyAsync(r => r.MatchId == matchId && r.TeamId == myTeamId.Value);

        bool canSubmit = true;
        string? reason = null;
        if (!OpenMatchStatuses.Contains(match.Status))
        {
            canSubmit = false;
            reason = "Trận đã có kết quả hoặc đang tranh chấp, không thể nộp báo cáo.";
        }
        else if (alreadyReported)
        {
            canSubmit = false;
            reason = "Đội của bạn đã nộp báo cáo cho trận này.";
        }
        else if (ReportDeadline.HasPassed(match.ReportDeadlineAt, DateTime.UtcNow))
        {
            // UC-27 E3 / UC-29: cửa sổ báo cáo đã đóng.
            canSubmit = false;
            reason = ReportDeadline.ClosedMessage;
        }
        else if (opponentTeamId is null)
        {
            canSubmit = false;
            reason = "Trận chưa xác định đủ hai đội.";
        }

        return new ReportSubmissionPageDto
        {
            MatchId = match.MatchId,
            TournamentId = match.TournamentId,
            TournamentName = match.Tournament.Name,
            BestOf = match.BestOf,
            MyTeamName = (myTeamId == match.TeamAid ? match.TeamA?.Name : match.TeamB?.Name) ?? "",
            OpponentTeamName = (opponentTeamId == match.TeamAid ? match.TeamA?.Name : match.TeamB?.Name) ?? "— chưa xác định —",
            MatchStatus = match.Status,
            ReportDeadlineAt = match.ReportDeadlineAt,
            AlreadyReported = alreadyReported,
            CanSubmit = canSubmit,
            CannotSubmitReason = reason
        };
    }

    public async Task<Result> SubmitReportAsync(int matchId, int captainAccountId, SubmitReportRequest request,
        List<(string Url, string FileType, long? SizeBytes)> evidenceFiles)
    {
        var match = await _uow.Repository<Match>().GetByIdAsync(matchId);
        if (match is null) return Result.Failure("Không tìm thấy trận đấu.");
        if (!OpenMatchStatuses.Contains(match.Status))
            return Result.Failure("Trận đã có kết quả hoặc đang tranh chấp, không thể nộp báo cáo.");

        var myTeamId = await ResolveCaptainTeamIdAsync(match, captainAccountId);
        if (myTeamId is null) return Result.Failure("Bạn không phải đội trưởng của đội nào trong trận này.");

        // UC-30 A2: kết quả trận trước (đưa đội vào trận này) đang bị tranh chấp → trận này tạm đóng băng.
        if (await IsFrozenByUpstreamDisputeAsync(matchId))
            return Result.Failure(FrozenMessage, StatusCodes.Status409Conflict);

        // E1 (MSG-RES-001) — BR-RES-01: đúng một báo cáo cho một đội trong một trận, không sửa/rút lại.
        if (await _uow.Repository<MatchReport>().AnyAsync(r => r.MatchId == matchId && r.TeamId == myTeamId.Value))
            return Result.Failure("Đội của bạn đã nộp báo cáo cho trận này.");

        // E3 (MSG-MAT-002): hết hạn trong lúc đội trưởng đang điền form → từ chối, UC-29 tiếp quản.
        if (ReportDeadline.HasPassed(match.ReportDeadlineAt, DateTime.UtcNow))
            return Result.Failure(ReportDeadline.ClosedMessage);

        // E2 (MSG-RES-003)
        if (evidenceFiles.Count == 0)
            return Result.Failure("Vui lòng đính kèm ảnh hoặc file minh chứng.");

        // Quy đổi góc nhìn đội trưởng (đội tôi/đội đối thủ) sang đúng cột TeamA/TeamB của Match.
        int scoreA, scoreB;
        if (myTeamId == match.TeamAid) { scoreA = request.MyScore; scoreB = request.OpponentScore; }
        else { scoreA = request.OpponentScore; scoreB = request.MyScore; }

        var report = new MatchReport
        {
            MatchId = matchId,
            TeamId = myTeamId.Value,
            ReportedByAccountId = captainAccountId,
            ReportedScoreA = scoreA,
            ReportedScoreB = scoreB,
            PerGameScores = request.PerGameScores,
            Status = "SUBMITTED"
        };
        await _uow.Repository<MatchReport>().AddAsync(report);
        await _uow.SaveChangesAsync(); // cần MatchReportId cho Attachment

        foreach (var f in evidenceFiles)
        {
            await _uow.Repository<Attachment>().AddAsync(new Attachment
            {
                OwnerType = "MATCH_REPORT",
                OwnerId = report.MatchReportId,
                FileUrl = f.Url,
                FileType = f.FileType,
                SizeBytes = f.SizeBytes,
                UploadedByAccountId = captainAccountId
            });
        }

        if (match.Status != "WAITING_REPORT")
        {
            match.Status = "WAITING_REPORT";
            _uow.Repository<Match>().Update(match);
        }
        await _uow.SaveChangesAsync();

        var reportCount = await _uow.Repository<MatchReport>().Query().CountAsync(r => r.MatchId == matchId);
        if (reportCount >= 2)
        {
            var sem = MatchReconcileLock.Get(matchId);
            await sem.WaitAsync();
            try { await ReconcileAsync(matchId); }
            finally { sem.Release(); }

            var finalMatch = await _uow.Repository<Match>().GetByIdAsync(matchId);
            if (finalMatch?.Status == "DISPUTED")
                return Result.Success("Đã nộp báo cáo. Hai báo cáo lệch nhau — trận đấu đã bị đóng băng và chuyển cho Trọng tài.");
            return Result.Success("Đã nộp báo cáo. Hai báo cáo khớp nhau — trận đấu đã được xác nhận tự động.");
        }

        return Result.Success("Đã nộp báo cáo thành công. Chờ đội đối thủ nộp báo cáo.");
    }

    // ===== RES-02 (UC-28) =====

    public async Task ReconcileAsync(int matchId)
    {
        var match = await _uow.Repository<Match>().GetByIdAsync(matchId);
        if (match is null || match.Status != "WAITING_REPORT") return;

        var reports = await _uow.Repository<MatchReport>().Query()
            .Where(r => r.MatchId == matchId).ToListAsync();
        if (reports.Count < 2) return;

        var repA = reports[0];
        var repB = reports[1];
        bool agree = repA.ReportedScoreA == repB.ReportedScoreA && repA.ReportedScoreB == repB.ReportedScoreB;

        if (agree)
        {
            match.ScoreA = repA.ReportedScoreA;
            match.ScoreB = repA.ReportedScoreB;
            match.WinnerTeamId = repA.ReportedScoreA > repA.ReportedScoreB ? match.TeamAid : match.TeamBid;
            match.Status = "VERIFIED";
            match.ResultSource = "AUTO_MATCHED";
            match.VerifiedAt = DateTime.UtcNow;
            _uow.Repository<Match>().Update(match);

            foreach (var r in reports) { r.Status = "MATCHED"; _uow.Repository<MatchReport>().Update(r); }

            await _uow.Repository<AuditLog>().AddAsync(new AuditLog
            {
                ActorAccountId = null,
                TournamentId = match.TournamentId,
                ActionType = "AUTO_VERIFY",
                ObjectType = "MATCH",
                ObjectId = match.MatchId,
                AfterValue = $"ScoreA={match.ScoreA};ScoreB={match.ScoreB};WinnerTeamId={match.WinnerTeamId}",
                Reason = "Hai báo cáo mù khớp nhau."
            });

            await _uow.SaveChangesAsync();
            if (match.WinnerTeamId.HasValue)
                await AdvanceWinnerAsync(match, match.WinnerTeamId.Value);
        }
        else
        {
            match.Status = "DISPUTED";
            _uow.Repository<Match>().Update(match);
            foreach (var r in reports) { r.Status = "CONFLICTING"; _uow.Repository<MatchReport>().Update(r); }

            var dispute = new Dispute
            {
                MatchId = match.MatchId,
                OpenReason = "CONFLICTING_REPORT",
                Status = "OPEN"
            };
            await _uow.Repository<Dispute>().AddAsync(dispute);
            await _uow.SaveChangesAsync();
            // UC-28 A1: báo trọng tài của trận, không có thì Ban tổ chức.
            await NotifyAdjudicatorAsync(match, "Tranh chấp mới: hai báo cáo lệch nhau",
                $"Trận #{match.MatchId}: hai đội trưởng báo tỉ số khác nhau. Trận đã bị khóa, mở hồ sơ tranh chấp để phân xử.", dispute.DisputeId);
            await _uow.SaveChangesAsync();
        }
    }

    // ===== RES-03 (UC-29) — đóng cửa sổ báo cáo khi quá hạn =====
    // Trận đang đá (READY/LIVE, chưa có báo cáo) hoặc đã có đúng một báo cáo (WAITING_REPORT).
    // PENDING không thuộc job này: chưa đủ hai đội check-in là việc của UC-23 (xử thua do vắng mặt).
    private static readonly string[] ReportableStatuses = { "READY", "LIVE", "WAITING_REPORT" };

    public async Task<IReadOnlyList<int>> GetOverdueReportMatchIdsAsync(DateTime nowUtc, CancellationToken ct = default)
    {
        // Chỉ trận CÓ hạn: trận xếp giờ trước khi có RES-03 không có hạn nên không bị tự đóng.
        return await _uow.Repository<Match>().Query()
            .Where(m => m.ReportDeadlineAt != null && m.ReportDeadlineAt <= nowUtc && ReportableStatuses.Contains(m.Status))
            .OrderBy(m => m.ReportDeadlineAt)
            .Select(m => m.MatchId)
            .Take(100)
            .ToListAsync(ct);
    }

    // Trả về true khi đã đóng cửa sổ và mở tranh chấp. Gọi trong MatchReconcileLock để không chen với
    // lần đối chiếu của RES-02 khi đội thứ hai nộp đúng lúc hết hạn.
    public async Task<bool> CloseReportWindowAsync(int matchId, DateTime nowUtc, CancellationToken ct = default)
    {
        var sem = MatchReconcileLock.Get(matchId);
        await sem.WaitAsync(ct);
        try
        {
            var match = await _uow.Repository<Match>().Query()
                .Include(m => m.Tournament).Include(m => m.TeamA).Include(m => m.TeamB)
                .FirstOrDefaultAsync(m => m.MatchId == matchId, ct);
            // E1: giữa lúc hết hạn và lúc job chạy, trận đã được phân xử/đổi trạng thái → để nguyên.
            if (match is null || !ReportableStatuses.Contains(match.Status)
                || !ReportDeadline.HasPassed(match.ReportDeadlineAt, nowUtc))
                return false;

            var reports = await _uow.Repository<MatchReport>().Query()
                .Where(r => r.MatchId == matchId).ToListAsync(ct);
            if (reports.Count >= 2) return false; // đủ hai bản thì RES-02 đối chiếu, không phải quá hạn

            // BR-RES-07: mỗi trận chỉ một tranh chấp đang mở.
            if (await _uow.Repository<Dispute>().AnyAsync(d => d.MatchId == matchId && d.Status != "RESOLVED" && d.Status != "CLOSED"))
                return false;

            match.Status = "DISPUTED";
            _uow.Repository<Match>().Update(match);
            // A1: không đội nào nộp thì tranh chấp mở mà không kèm báo cáo nào.
            var dispute = new Dispute { MatchId = matchId, OpenReason = "REPORT_TIMEOUT", Status = "OPEN", AppealStatus = "NONE", OpenedAt = nowUtc };
            await _uow.Repository<Dispute>().AddAsync(dispute);
            await _uow.SaveChangesAsync(); // cần DisputeId cho đường dẫn trong thông báo

            await NotifyReportTimeoutAsync(match, reports, dispute.DisputeId, nowUtc);
            await _uow.SaveChangesAsync();
            return true;
        }
        finally { sem.Release(); }
    }

    private async Task NotifyReportTimeoutAsync(Match match, List<MatchReport> reports, int disputeId, DateTime nowUtc)
    {
        string teamA = match.TeamA?.Name ?? "Đội A", teamB = match.TeamB?.Name ?? "Đội B";
        string label = $"{teamA} vs {teamB} ({match.Tournament.Name})";
        string deadline = match.ReportDeadlineAt!.Value.AddHours(7).ToString("HH:mm dd/MM/yyyy");

        // Bước 4: nói rõ trận dừng vì THIẾU báo cáo, và đội nào thiếu.
        string missing = reports.Count == 0
            ? "cả hai đội đều chưa nộp báo cáo kết quả"
            : $"đội {(reports[0].TeamId == match.TeamAid ? teamB : teamA)} chưa nộp báo cáo kết quả " +
              $"(đội {(reports[0].TeamId == match.TeamAid ? teamA : teamB)} đã nộp)";
        string adjudicatorRole = match.RefereeAccountId.HasValue ? "Trọng tài" : "Ban tổ chức";

        var sent = new HashSet<int>();
        async Task Send(int? accountId, string title, string body, string link)
        {
            if (accountId is not int id || !sent.Add(id)) return;
            await _uow.Repository<Notification>().AddAsync(new Notification
            {
                AccountId = id,
                NotificationType = "DISPUTE",
                Title = title,
                Body = body.Length > 1000 ? body[..1000] : body,
                LinkUrl = link,
                Channel = "IN_APP",
                IsRead = false,
                SentAt = nowUtc
            });
        }

        // Bước 3: báo người phân xử — Trọng tài được gán, không có thì Ban tổ chức.
        int adjudicator = match.RefereeAccountId ?? match.Tournament.OrganizerAccountId;
        await Send(adjudicator, "Tranh chấp mới: quá hạn báo cáo",
            $"Trận {label} đã hết hạn báo cáo lúc {deadline} nhưng {missing}. Trận đã chuyển sang tranh chấp, cần bạn phân xử.",
            "/result/dispute.html?id=" + disputeId);
        // Hậu điều kiện: Ban tổ chức luôn được báo, kể cả khi trận đã có trọng tài.
        await Send(match.Tournament.OrganizerAccountId, "Trận quá hạn báo cáo",
            $"Trận {label} đã hết hạn báo cáo lúc {deadline} nhưng {missing}. Trận đã chuyển cho {adjudicatorRole} phân xử.",
            "/result/evidence.html?matchId=" + match.MatchId);

        var captainBody = $"Trận {label} đã dừng vì thiếu báo cáo: hết hạn lúc {deadline} nhưng {missing}. " +
                          $"Báo cáo thiếu không tự quyết định kết quả — {adjudicatorRole} sẽ phân xử.";
        await Send(match.TeamA?.CaptainAccountId, "Trận đã dừng vì thiếu báo cáo", captainBody, "/result/evidence.html?matchId=" + match.MatchId);
        await Send(match.TeamB?.CaptainAccountId, "Trận đã dừng vì thiếu báo cáo", captainBody, "/result/evidence.html?matchId=" + match.MatchId);
    }

    // Đẩy đội thắng lên vị trí ở vòng trên — cùng logic với BracketService (trước đây là cầu nối
    // tạm PickWinner, giờ RES-02/RES-07 gọi trực tiếp vì đã có luồng kết quả thật).
    private async Task AdvanceWinnerAsync(Match match, int winnerTeamId)
    {
        var node = await _uow.Repository<BracketNode>().GetByIdAsync(match.BracketNodeId);
        if (node is null) return;
        if (node.StageType == "KNOCKOUT" && DoubleElimination.IsDoubleElimination(await _uow.Repository<Tournament>().Query()
                .Where(t => t.TournamentId == match.TournamentId).Select(t => t.Format).FirstOrDefaultAsync()))
        {
            await AdvanceDoubleEliminationAsync(match, node, winnerTeamId);
            return;
        }
        if (node.ParentNodeId is null) return;

        var parentMatch = await _uow.Repository<Match>().Query()
            .FirstOrDefaultAsync(m => m.BracketNodeId == node.ParentNodeId.Value);
        if (parentMatch is null) return;

        bool isLeftChild = (node.PositionInRound ?? 1) % 2 == 1;
        if (isLeftChild) parentMatch.TeamAid = winnerTeamId; else parentMatch.TeamBid = winnerTeamId;
        _uow.Repository<Match>().Update(parentMatch);
        await _uow.SaveChangesAsync();
    }

    // BRK-05 Loại kép: đội thắng đi tiếp theo ParentNodeId; đội thua ở nhánh thắng rơi xuống nhánh thua
    // (thua ở nhánh thua hoặc chung kết tổng là bị loại). Ô đích tính xác định (DoubleElimination.Layout), nên
    // phán quyết lại (VAR, khiếu nại) chỉ ghi đè đúng hai ô đó.
    private async Task AdvanceDoubleEliminationAsync(Match match, BracketNode node, int winnerTeamId)
    {
        var nodes = await _uow.Repository<BracketNode>().Query()
            .Where(n => n.TournamentId == match.TournamentId && n.StageType == "KNOCKOUT").ToListAsync();
        var layout = new DoubleElimination.Layout(nodes);
        int? loserTeamId = match.TeamAid == winnerTeamId ? match.TeamBid : match.TeamAid;

        async Task Place(DoubleElimination.Slot? slot, int teamId)
        {
            if (slot is null) return;
            var target = await _uow.Repository<Match>().Query().FirstOrDefaultAsync(m => m.BracketNodeId == slot.Node.BracketNodeId);
            if (target is null) return;
            if (slot.SideA) target.TeamAid = teamId; else target.TeamBid = teamId;
            _uow.Repository<Match>().Update(target);
        }

        await Place(layout.WinnerTo(node), winnerTeamId);
        if (loserTeamId is int loser) await Place(layout.LoserTo(node), loser);
        await _uow.SaveChangesAsync();
    }

    // ===== UC-30 A2: đóng băng vị trí phía sau của trận đang tranh chấp =====
    public const string FrozenMessage = "Kết quả trận trước đưa đội vào trận này đang bị tranh chấp, trận tạm đóng băng cho tới khi có phán quyết.";

    public async Task<bool> IsFrozenByUpstreamDisputeAsync(int matchId)
    {
        var tid = await _uow.Repository<Match>().Query().Where(m => m.MatchId == matchId).Select(m => (int?)m.TournamentId).FirstOrDefaultAsync();
        if (tid is null) return false;
        var disputed = await _uow.Repository<Match>().Query()
            .Where(m => m.TournamentId == tid && m.Status == "DISPUTED" && m.WinnerTeamId != null && m.MatchId != matchId)
            .ToListAsync();
        foreach (var d in disputed)
            if ((await DownstreamSlotsAsync(d)).Any(s => s.Target.MatchId == matchId)) return true;
        return false;
    }

    // UC-28 A1 / UC-30 bước 4: báo người phân xử — trọng tài của trận, không có thì Ban tổ chức.
    private async Task NotifyAdjudicatorAsync(Match match, string title, string body, int disputeId)
    {
        var organizerId = await _uow.Repository<Tournament>().Query().Where(t => t.TournamentId == match.TournamentId)
            .Select(t => t.OrganizerAccountId).FirstAsync();
        await _uow.Repository<Notification>().AddAsync(new Notification
        {
            AccountId = match.RefereeAccountId ?? organizerId,
            NotificationType = "DISPUTE",
            Title = title,
            Body = body.Length > 1000 ? body[..1000] : body,
            LinkUrl = "/result/dispute.html?id=" + disputeId,
            Channel = "IN_APP",
            IsRead = false,
            SentAt = DateTime.UtcNow
        });
    }

    // ===== UC-26: gỡ kết quả đã đẩy sang trận sau =====
    private static bool Played(Match m) =>
        m.WinnerTeamId != null || m.ScoreA != null || m.ScoreB != null || (m.Status != "PENDING" && m.Status != "READY");

    public async Task<IReadOnlyList<Match>> RevertAdvancementAsync(int matchId, bool apply)
    {
        var reset = new List<Match>();
        await RevertAsync(matchId, apply, reset, new HashSet<int>());
        if (apply) await _uow.SaveChangesAsync();
        return reset;
    }

    // Các ô (trận, phía A?, đội) mà kết quả của trận này đã ghi đội vào.
    private async Task<List<(Match Target, bool SideA, int TeamId)>> DownstreamSlotsAsync(Match match)
    {
        var slots = new List<(Match, bool, int)>();
        if (match.WinnerTeamId is not int winner) return slots;
        int? loser = match.TeamAid == winner ? match.TeamBid : match.TeamAid;
        var node = await _uow.Repository<BracketNode>().GetByIdAsync(match.BracketNodeId);
        if (node is null || node.StageType != "KNOCKOUT") return slots;   // vòng bảng: không đẩy ai, bảng điểm tự tính lại

        async Task Add(int nodeId, bool sideA, int team)
        {
            var t = await _uow.Repository<Match>().Query().FirstOrDefaultAsync(m => m.BracketNodeId == nodeId);
            if (t is not null && (sideA ? t.TeamAid : t.TeamBid) == team) slots.Add((t, sideA, team));
        }

        var format = await _uow.Repository<Tournament>().Query().Where(t => t.TournamentId == match.TournamentId).Select(t => t.Format).FirstOrDefaultAsync();
        if (DoubleElimination.IsDoubleElimination(format))
        {
            var nodes = await _uow.Repository<BracketNode>().Query()
                .Where(n => n.TournamentId == match.TournamentId && n.StageType == "KNOCKOUT").ToListAsync();
            var layout = new DoubleElimination.Layout(nodes);
            if (layout.WinnerTo(node) is { } w) await Add(w.Node.BracketNodeId, w.SideA, winner);
            if (loser is int l && layout.LoserTo(node) is { } ls) await Add(ls.Node.BracketNodeId, ls.SideA, l);
            return slots;
        }
        if (node.ParentNodeId is int parentId)
        {
            await Add(parentId, (node.PositionInRound ?? 1) % 2 == 1, winner);
            // Đội thua bán kết vào trận tranh hạng Ba (cùng vòng với chung kết, vị trí 2, không có nút cha).
            var parent = await _uow.Repository<BracketNode>().GetByIdAsync(parentId);
            if (loser is int l && parent is not null && parent.ParentNodeId is null)
            {
                var third = await _uow.Repository<BracketNode>().Query().FirstOrDefaultAsync(n => n.TournamentId == match.TournamentId
                    && n.StageType == "KNOCKOUT" && n.StageNo == parent.StageNo && n.RoundNo == parent.RoundNo
                    && n.PositionInRound == 2 && n.ParentNodeId == null);
                if (third is not null)
                {
                    var tm = await _uow.Repository<Match>().Query().FirstOrDefaultAsync(m => m.BracketNodeId == third.BracketNodeId);
                    if (tm is not null && tm.TeamAid == l) slots.Add((tm, true, l));
                    else if (tm is not null && tm.TeamBid == l) slots.Add((tm, false, l));
                }
            }
        }
        return slots;
    }

    private async Task RevertAsync(int matchId, bool apply, List<Match> reset, HashSet<int> seen)
    {
        if (!seen.Add(matchId)) return;
        var match = await _uow.Repository<Match>().GetByIdAsync(matchId);
        if (match is null) return;
        foreach (var (target, sideA, _) in await DownstreamSlotsAsync(match))
        {
            if (Played(target))
            {
                // A2: trận phía sau đã đá trên kết quả sắp hủy → hủy luôn (đệ quy trước khi xóa đội thắng của nó).
                await RevertAsync(target.MatchId, apply, reset, seen);
                if (reset.All(r => r.MatchId != target.MatchId)) reset.Add(target);
                if (apply)
                {
                    foreach (var r in await _uow.Repository<MatchReport>().Query().Where(r => r.MatchId == target.MatchId).ToListAsync())
                        _uow.Repository<MatchReport>().Remove(r);
                    target.ScoreA = null; target.ScoreB = null; target.WinnerTeamId = null;
                    target.ResultSource = null; target.VerifiedAt = null;
                    target.Status = "PENDING";
                }
            }
            if (apply)
            {
                if (sideA) target.TeamAid = null; else target.TeamBid = null;
                target.TeamAcheckedInAt = null; target.TeamBcheckedInAt = null;
                target.CheckInMode = "SELF"; target.OverrideReason = null;
                _uow.Repository<Match>().Update(target);
            }
        }
    }

    // ===== RES-04 + RES-09 (UC-39) =====

    public async Task<MatchEvidenceDto?> GetEvidenceAsync(int matchId, int? viewerAccountId)
    {
        var match = await _uow.Repository<Match>().Query()
            .Include(m => m.TeamA).Include(m => m.TeamB)
            .FirstOrDefaultAsync(m => m.MatchId == matchId);
        if (match is null) return null;

        var dto = new MatchEvidenceDto
        {
            MatchId = match.MatchId,
            TeamAName = match.TeamA?.Name ?? "",
            TeamBName = match.TeamB?.Name ?? "",
            MatchStatus = match.Status
        };

        var reports = await _uow.Repository<MatchReport>().Query()
            .Include(r => r.Team)
            .Where(r => r.MatchId == matchId).ToListAsync();

        // BR-RES-02: còn trong cửa sổ "mù" (chưa đủ 2 báo cáo và trận chưa đóng cửa sổ báo cáo) → không lộ gì.
        bool windowStillOpen = OpenMatchStatuses.Contains(match.Status);
        if (reports.Count < 2 && windowStillOpen)
        {
            dto.IsVisible = false;
            dto.HiddenReason = reports.Count == 0
                ? "Chưa có báo cáo nào được nộp."
                : "Đang chờ đội còn lại nộp báo cáo. Minh chứng chỉ công khai khi đủ hai báo cáo hoặc hết thời gian báo cáo.";
            return dto;
        }

        dto.IsVisible = true;
        dto.ClosedOnTimeout = await _uow.Repository<Dispute>()
            .AnyAsync(d => d.MatchId == matchId && d.OpenReason == "REPORT_TIMEOUT");
        foreach (var r in reports)
        {
            var files = await _uow.Repository<Attachment>().Query()
                .Where(a => a.OwnerType == "MATCH_REPORT" && a.OwnerId == r.MatchReportId)
                .Select(a => new EvidenceFileDto { FileUrl = a.FileUrl, FileType = a.FileType })
                .ToListAsync();
            dto.Reports.Add(new ReportEvidenceDto
            {
                TeamName = r.Team.Name,
                ScoreA = r.ReportedScoreA,
                ScoreB = r.ReportedScoreB,
                SubmittedAt = r.SubmittedAt,
                Files = files
            });
        }

        dto.CanFlag = viewerAccountId.HasValue && (match.Status == "VERIFIED" || match.Status == "WAITING_REPORT");
        return dto;
    }

    public async Task<MatchAuditTrailDto?> GetAuditTrailAsync(int matchId)
    {
        var match = await _uow.Repository<Match>().Query()
            .Include(m => m.TeamA).Include(m => m.TeamB)
            .FirstOrDefaultAsync(m => m.MatchId == matchId);
        if (match is null) return null;

        var dto = new MatchAuditTrailDto
        {
            MatchId = match.MatchId,
            TeamAName = match.TeamA?.Name ?? "",
            TeamBName = match.TeamB?.Name ?? ""
        };

        if (match.ResultSource == "AUTO_MATCHED" && match.VerifiedAt.HasValue)
        {
            dto.Events.Add(new AuditTrailEventDto
            {
                At = match.VerifiedAt.Value,
                Kind = "AUTO_VERIFY",
                Title = "Hai báo cáo mù khớp nhau — trận tự động xác nhận",
                Detail = $"Tỉ số {match.ScoreA}-{match.ScoreB}, đội thắng: {(match.WinnerTeamId == match.TeamAid ? match.TeamA?.Name : match.TeamB?.Name)}"
            });
        }

        var disputes = await _uow.Repository<Dispute>().Query()
            .Include(d => d.FlaggedByAccount).Include(d => d.ResolvedByAccount)
            .Where(d => d.MatchId == matchId).ToListAsync();

        foreach (var d in disputes)
        {
            if (d.OpenReason == "FLAGGED")
            {
                dto.Events.Add(new AuditTrailEventDto
                {
                    At = d.OpenedAt,
                    Kind = "FLAGGED",
                    Title = $"Bị cắm cờ nghi vấn ({FlagCategoryLabel(d.FlagCategory)})",
                    Detail = d.FlagNote,
                    ActorName = d.FlaggedByAccount?.FullName
                });
            }
            else if (d.OpenReason == "CONFLICTING_REPORT")
            {
                dto.Events.Add(new AuditTrailEventDto { At = d.OpenedAt, Kind = "DISPUTED", Title = "Hai báo cáo lệch nhau — mở tranh chấp" });
            }

            if (d.Status == "RESOLVED" || d.Status == "CLOSED")
            {
                if (d.ResolvedAt.HasValue)
                {
                    dto.Events.Add(new AuditTrailEventDto
                    {
                        At = d.ResolvedAt.Value,
                        Kind = "FORCE_UPDATE",
                        Title = $"Phán quyết VAR: {d.FinalScoreA}-{d.FinalScoreB}",
                        Detail = d.RulingReason,
                        ActorName = d.ResolvedByAccount?.FullName
                    });
                }
                if (d.AppealStatus is "ACCEPTED" or "REJECTED")
                {
                    dto.Events.Add(new AuditTrailEventDto
                    {
                        At = d.ResolvedAt ?? d.OpenedAt,
                        Kind = "APPEAL",
                        Title = d.AppealStatus == "ACCEPTED" ? "Khiếu nại được chấp nhận" : "Khiếu nại bị bác",
                        Detail = d.AppealOutcomeNote
                    });
                }
            }
        }

        dto.Events = dto.Events.OrderBy(e => e.At).ToList();

        dto.AppealableDisputeId = disputes
            .Where(d => d.Status == "RESOLVED" && d.AppealStatus == "NONE")
            .Select(d => (int?)d.DisputeId)
            .FirstOrDefault();

        return dto;
    }

    private static string FlagCategoryLabel(string? c) => c switch
    {
        "WRONG_SCORE" => "Sai tỉ số",
        "CHEATING" => "Gian lận",
        "INELIGIBLE_PLAYER" => "Cầu thủ không đủ điều kiện",
        _ => "Khác"
    };

    // ===== RES-05 (UC-30) =====

    public async Task<Result> FlagMatchAsync(int matchId, int accountId, FlagMatchRequest request)
    {
        var match = await _uow.Repository<Match>().GetByIdAsync(matchId);
        if (match is null) return Result.Failure("Không tìm thấy trận đấu.");
        if (match.Status != "VERIFIED" && match.Status != "WAITING_REPORT")
            return Result.Failure("Chỉ cắm cờ được trận đã có báo cáo/kết quả công khai.");

        // A1/BR-RES-07: tối đa 1 tranh chấp đang mở trên 1 trận.
        if (await _uow.Repository<Dispute>().AnyAsync(d => d.MatchId == matchId && (d.Status == "OPEN" || d.Status == "UNDER_REVIEW")))
            return Result.Failure("Trận này đang được xem xét. Không mở thêm tranh chấp thứ hai.");

        var dispute = new Dispute
        {
            MatchId = matchId,
            OpenReason = "FLAGGED",
            FlaggedByAccountId = accountId,
            FlagCategory = request.Category,
            FlagNote = request.Note,
            Status = "OPEN"
        };
        await _uow.Repository<Dispute>().AddAsync(dispute);

        match.Status = "DISPUTED";
        _uow.Repository<Match>().Update(match);
        await _uow.SaveChangesAsync();

        // UC-30 bước 4: báo người phân xử. A2: trận đã đẩy đội thắng đi thì trận phía sau tạm đóng băng
        // (IsFrozenByUpstreamDisputeAsync chặn điểm danh và nộp báo cáo ở đó cho tới khi có phán quyết).
        await NotifyAdjudicatorAsync(match, "Trận bị cắm cờ nghi vấn",
            $"Trận #{match.MatchId} bị cắm cờ ({FlagCategoryLabel(request.Category)}): {request.Note}", dispute.DisputeId);
        await _uow.SaveChangesAsync();

        return Result.Success("Đã ghi nhận cờ nghi vấn. Trận đấu đã bị đóng băng chờ xem xét.");
    }

    // ===== RES-06 (UC-31) =====

    public async Task<IReadOnlyList<DisputeListItemDto>> GetDisputeQueueAsync(int adjudicatorAccountId)
    {
        var disputes = await _uow.Repository<Dispute>().Query()
            .Include(d => d.Match).ThenInclude(m => m.TeamA)
            .Include(d => d.Match).ThenInclude(m => m.TeamB)
            .Include(d => d.Match).ThenInclude(m => m.Tournament)
            .Where(d => d.Status == "OPEN" || d.Status == "UNDER_REVIEW")
            .Where(d => d.Match.RefereeAccountId == adjudicatorAccountId
                     || (d.Match.RefereeAccountId == null && d.Match.Tournament.OrganizerAccountId == adjudicatorAccountId))
            .OrderBy(d => d.OpenedAt)
            .ToListAsync();

        return disputes.Select(d => new DisputeListItemDto
        {
            DisputeId = d.DisputeId,
            MatchId = d.MatchId,
            TournamentId = d.Match.TournamentId,
            TournamentName = d.Match.Tournament.Name,
            TeamAName = d.Match.TeamA?.Name ?? "",
            TeamBName = d.Match.TeamB?.Name ?? "",
            OpenReason = d.OpenReason,
            Status = d.Status,
            OpenedAt = d.OpenedAt
        }).ToList();
    }

    public async Task<bool?> IsAdjudicatorAsync(int disputeId, int accountId)
    {
        var dispute = await _uow.Repository<Dispute>().Query()
            .Include(d => d.Match).ThenInclude(m => m.Tournament)
            .FirstOrDefaultAsync(d => d.DisputeId == disputeId);
        if (dispute is null) return null;
        return dispute.Match.RefereeAccountId == accountId
            || (dispute.Match.RefereeAccountId == null && dispute.Match.Tournament.OrganizerAccountId == accountId);
    }

    public async Task<DisputeDetailDto?> GetDisputeDetailAsync(int disputeId, int viewerAccountId)
    {
        var dispute = await _uow.Repository<Dispute>().Query()
            .Include(d => d.Match).ThenInclude(m => m.TeamA)
            .Include(d => d.Match).ThenInclude(m => m.TeamB)
            .Include(d => d.FlaggedByAccount)
            .Include(d => d.ResolvedByAccount)
            .FirstOrDefaultAsync(d => d.DisputeId == disputeId);
        if (dispute is null) return null;

        // Mở hồ sơ thì chuyển sang Under Review (gắn với người đang xem) nếu còn ở OPEN.
        if (dispute.Status == "OPEN")
        {
            dispute.Status = "UNDER_REVIEW";
            _uow.Repository<Dispute>().Update(dispute);
            await _uow.SaveChangesAsync();
        }

        var match = dispute.Match;
        var reports = await _uow.Repository<MatchReport>().Query()
            .Include(r => r.Team)
            .Where(r => r.MatchId == match.MatchId).ToListAsync();

        var reportDtos = new List<ReportEvidenceDto>();
        foreach (var r in reports)
        {
            var files = await _uow.Repository<Attachment>().Query()
                .Where(a => a.OwnerType == "MATCH_REPORT" && a.OwnerId == r.MatchReportId)
                .Select(a => new EvidenceFileDto { FileUrl = a.FileUrl, FileType = a.FileType })
                .ToListAsync();
            reportDtos.Add(new ReportEvidenceDto
            {
                TeamName = r.Team.Name, ScoreA = r.ReportedScoreA, ScoreB = r.ReportedScoreB,
                SubmittedAt = r.SubmittedAt, Files = files
            });
        }

        var chat = await _uow.Repository<MatchChatMessage>().Query()
            .Include(c => c.SenderAccount)
            .Where(c => c.MatchId == match.MatchId)
            .OrderBy(c => c.SentAt)
            .Select(c => new ChatLogItemDto { SenderName = c.SenderAccount.FullName, Content = c.Body, SentAt = c.SentAt })
            .ToListAsync();

        return new DisputeDetailDto
        {
            DisputeId = dispute.DisputeId,
            MatchId = match.MatchId,
            TournamentId = match.TournamentId,
            TeamAName = match.TeamA?.Name ?? "", TeamAId = match.TeamAid ?? 0,
            TeamBName = match.TeamB?.Name ?? "", TeamBId = match.TeamBid ?? 0,
            OpenReason = dispute.OpenReason,
            Status = dispute.Status,
            FlagCategory = dispute.FlagCategory,
            FlagNote = dispute.FlagNote,
            FlaggedByName = dispute.FlaggedByAccount?.FullName,
            Reports = reportDtos,
            ChatLog = chat,
            FinalScoreA = dispute.FinalScoreA,
            FinalScoreB = dispute.FinalScoreB,
            RulingReason = dispute.RulingReason,
            ResolvedByName = dispute.ResolvedByAccount?.FullName,
            ResolvedAt = dispute.ResolvedAt,
            AppealCount = dispute.AppealCount,
            AppealReason = dispute.AppealReason,
            AppealStatus = dispute.AppealStatus,
            AppealOutcomeNote = dispute.AppealOutcomeNote,
            CanRule = dispute.Status == "UNDER_REVIEW"
        };
    }

    // ===== RES-07 (UC-32) =====

    public async Task<Result> ForceUpdateAsync(int disputeId, int adjudicatorAccountId, ForceUpdateRequest request)
    {
        var dispute = await _uow.Repository<Dispute>().Query()
            .Include(d => d.Match)
            .FirstOrDefaultAsync(d => d.DisputeId == disputeId);
        if (dispute is null) return Result.Failure("Không tìm thấy tranh chấp.");
        if (dispute.Status != "UNDER_REVIEW") return Result.Failure("Tranh chấp không ở trạng thái đang xem xét.");
        if (await IsAdjudicatorAsync(disputeId, adjudicatorAccountId) != true)
            return Result.Failure("Chỉ Trọng tài được gán hoặc Ban tổ chức của giải được phán quyết.");

        // E1/MSG-RES-004 — cũng được CHECK constraint CK_Dispute_Ruling chặn ở tầng DB (BR-RES-04).
        if (string.IsNullOrWhiteSpace(request.Reason))
            return Result.FieldFailure(nameof(request.Reason), "Vui lòng nhập lý do phán quyết.");

        var match = dispute.Match;

        dispute.Status = "RESOLVED";
        dispute.FinalScoreA = request.ScoreA;
        dispute.FinalScoreB = request.ScoreB;
        dispute.RulingReason = request.Reason;
        dispute.ResolvedByAccountId = adjudicatorAccountId;
        dispute.ResolvedAt = DateTime.UtcNow;
        _uow.Repository<Dispute>().Update(dispute);

        var before = $"Status={match.Status};ScoreA={match.ScoreA};ScoreB={match.ScoreB};WinnerTeamId={match.WinnerTeamId}";

        if (request.IsVoid)
        {
            // A1: không đội nào thắng — hủy kết quả, để Ban tổ chức xử lý tiếp (BR-RES-08 không cho VERIFIED khi không có đội thắng).
            match.Status = "CANCELLED";
            match.ScoreA = null; match.ScoreB = null; match.WinnerTeamId = null;
            match.ResultSource = "VAR_RULING";
            match.VerifiedAt = DateTime.UtcNow;
        }
        else
        {
            match.ScoreA = request.ScoreA;
            match.ScoreB = request.ScoreB;
            match.WinnerTeamId = request.ScoreA > request.ScoreB ? match.TeamAid : match.TeamBid;
            match.Status = "VERIFIED";
            match.ResultSource = "VAR_RULING";
            match.VerifiedAt = DateTime.UtcNow;
        }
        _uow.Repository<Match>().Update(match);

        await _uow.Repository<AuditLog>().AddAsync(new AuditLog
        {
            ActorAccountId = adjudicatorAccountId,
            TournamentId = match.TournamentId,
            ActionType = "FORCE_UPDATE",
            ObjectType = "MATCH",
            ObjectId = match.MatchId,
            BeforeValue = before,
            AfterValue = $"Status={match.Status};ScoreA={match.ScoreA};ScoreB={match.ScoreB};WinnerTeamId={match.WinnerTeamId}",
            Reason = request.Reason
        });

        await _uow.SaveChangesAsync();

        if (!request.IsVoid && match.WinnerTeamId.HasValue)
            await AdvanceWinnerAsync(match, match.WinnerTeamId.Value);

        // UC-19 A3 / UC-33 A1: giải đã kết thúc mà kết quả đổi → tính lại hạng, danh hiệu và điểm.
        var recomputed = await _services.GetRequiredService<ITournamentLifecycleService>().RecomputeCompletedAsync(match.TournamentId);
        return Result.Success(recomputed ? "Đã ghi nhận phán quyết VAR. Thứ hạng và điểm của giải đã được tính lại." : "Đã ghi nhận phán quyết VAR.");
    }

    // ===== RES-08 (UC-33) =====

    public async Task<Result> AppealAsync(int disputeId, int captainAccountId, AppealRequest request)
    {
        var dispute = await _uow.Repository<Dispute>().Query()
            .Include(d => d.Match)
            .FirstOrDefaultAsync(d => d.DisputeId == disputeId);
        if (dispute is null) return Result.Failure("Không tìm thấy phán quyết.");
        if (dispute.Status != "RESOLVED") return Result.Failure("Chỉ khiếu nại được phán quyết đã có kết luận.");

        var captainTeamId = await ResolveCaptainTeamIdAsync(dispute.Match, captainAccountId);
        if (captainTeamId is null) return Result.Failure("Bạn không phải đội trưởng của đội nào trong trận này.");

        // E2/MSG-RES-005 — cũng được CK_Dispute_AppealN (0–1) chặn ở tầng DB (BR-RES-05).
        if (dispute.AppealCount >= 1)
            return Result.Failure("Phán quyết này đã được khiếu nại một lần.");

        dispute.AppealCount = 1;
        dispute.AppealReason = request.Reason;
        dispute.AppealStatus = "PENDING";
        dispute.Status = "UNDER_REVIEW";
        _uow.Repository<Dispute>().Update(dispute);
        await _uow.SaveChangesAsync();

        return Result.Success("Đã gửi khiếu nại. Trọng tài/Ban tổ chức sẽ xem xét lại.");
    }

    public async Task<Result> DecideAppealAsync(int disputeId, int adjudicatorAccountId, AppealDecisionRequest request)
    {
        var dispute = await _uow.Repository<Dispute>().Query()
            .Include(d => d.Match)
            .FirstOrDefaultAsync(d => d.DisputeId == disputeId);
        if (dispute is null) return Result.Failure("Không tìm thấy phán quyết.");
        if (dispute.AppealStatus != "PENDING") return Result.Failure("Không có khiếu nại nào đang chờ xử lý.");
        if (await IsAdjudicatorAsync(disputeId, adjudicatorAccountId) != true)
            return Result.Failure("Chỉ Trọng tài được gán hoặc Ban tổ chức của giải được kết luận khiếu nại.");

        // E1/BR-RES-09 — cũng được CK_Dispute_AppealOut chặn ở tầng DB.
        if (string.IsNullOrWhiteSpace(request.OutcomeNote))
            return Result.FieldFailure(nameof(request.OutcomeNote), "Vui lòng nhập kết luận khiếu nại.");

        if (request.Upheld)
        {
            if (request.NewScoreA is null || request.NewScoreB is null)
                return Result.FieldFailure(nameof(request.NewScoreA), "Chấp nhận khiếu nại thì phải nhập tỉ số mới.");

            // Gọi ForceUpdateAsync TRƯỚC khi đổi dispute.Status: hàm đó chỉ chấp nhận tranh chấp
            // đang ở UNDER_REVIEW (chính trạng thái hiện tại), và tự set Status=RESOLVED khi xong.
            // Nếu đổi sang CLOSED trước rồi mới gọi, ForceUpdateAsync luôn thất bại — tỉ số mới
            // không bao giờ được áp dụng dù khiếu nại đã lưu là ACCEPTED (không gọi lại được nữa
            // vì AppealStatus khi đó không còn PENDING).
            var forceUpdate = await ForceUpdateAsync(disputeId, adjudicatorAccountId, new ForceUpdateRequest
            {
                ScoreA = request.NewScoreA.Value,
                ScoreB = request.NewScoreB.Value,
                Reason = $"[Khiếu nại được chấp nhận] {request.OutcomeNote}"
            });
            if (!forceUpdate.IsSuccess) return forceUpdate;

            dispute.AppealStatus = "ACCEPTED";
            dispute.AppealOutcomeNote = request.OutcomeNote;
            dispute.Status = "CLOSED";
            _uow.Repository<Dispute>().Update(dispute);
            await _uow.SaveChangesAsync();
        }
        else
        {
            dispute.AppealStatus = "REJECTED";
            dispute.AppealOutcomeNote = request.OutcomeNote;
            dispute.Status = "CLOSED";
            _uow.Repository<Dispute>().Update(dispute);
            await _uow.SaveChangesAsync();
        }

        await _uow.Repository<AuditLog>().AddAsync(new AuditLog
        {
            ActorAccountId = adjudicatorAccountId,
            TournamentId = dispute.Match.TournamentId,
            ActionType = "APPEAL_DECISION",
            ObjectType = "DISPUTE",
            ObjectId = dispute.DisputeId,
            AfterValue = $"AppealStatus={dispute.AppealStatus}",
            Reason = request.OutcomeNote
        });
        await _uow.SaveChangesAsync();

        return Result.Success(request.Upheld ? "Đã chấp nhận khiếu nại, cập nhật tỉ số mới." : "Đã bác khiếu nại.");
    }

    // ===== Dùng chung =====

    public async Task<bool> IsMatchCaptainAsync(int matchId, int accountId)
    {
        var match = await _uow.Repository<Match>().GetByIdAsync(matchId);
        if (match is null) return false;
        return await ResolveCaptainTeamIdAsync(match, accountId) is not null;
    }

    public async Task<int?> GetCaptainTeamIdAsync(int matchId, int accountId)
    {
        var match = await _uow.Repository<Match>().GetByIdAsync(matchId);
        return match is null ? null : await ResolveCaptainTeamIdAsync(match, accountId);
    }

    public async Task AdvanceWinnerAsync(int matchId, int winnerTeamId)
    {
        var match = await _uow.Repository<Match>().GetByIdAsync(matchId);
        if (match is not null) await AdvanceWinnerAsync(match, winnerTeamId);
    }

    private async Task<int?> ResolveCaptainTeamIdAsync(Match match, int accountId)
    {
        if (match.TeamAid.HasValue && await _uow.Repository<Team>().AnyAsync(t => t.TeamId == match.TeamAid.Value && t.CaptainAccountId == accountId))
            return match.TeamAid;
        if (match.TeamBid.HasValue && await _uow.Repository<Team>().AnyAsync(t => t.TeamId == match.TeamBid.Value && t.CaptainAccountId == accountId))
            return match.TeamBid;
        return null;
    }
}
