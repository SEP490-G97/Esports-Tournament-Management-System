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

    public ResultService(IUnitOfWork uow) => _uow = uow;

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

        // E1 (MSG-RES-001) — BR-RES-01: đúng một báo cáo cho một đội trong một trận, không sửa/rút lại.
        if (await _uow.Repository<MatchReport>().AnyAsync(r => r.MatchId == matchId && r.TeamId == myTeamId.Value))
            return Result.Failure("Đội của bạn đã nộp báo cáo cho trận này.");

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

            await _uow.Repository<Dispute>().AddAsync(new Dispute
            {
                MatchId = match.MatchId,
                OpenReason = "CONFLICTING_REPORT",
                Status = "OPEN"
            });
            await _uow.SaveChangesAsync();
        }
    }

    // Đẩy đội thắng lên vị trí ở vòng trên — cùng logic với BracketService (trước đây là cầu nối
    // tạm PickWinner, giờ RES-02/RES-07 gọi trực tiếp vì đã có luồng kết quả thật).
    private async Task AdvanceWinnerAsync(Match match, int winnerTeamId)
    {
        var node = await _uow.Repository<BracketNode>().GetByIdAsync(match.BracketNodeId);
        if (node?.ParentNodeId is null) return;

        var parentMatch = await _uow.Repository<Match>().Query()
            .FirstOrDefaultAsync(m => m.BracketNodeId == node.ParentNodeId.Value);
        if (parentMatch is null) return;

        bool isLeftChild = (node.PositionInRound ?? 1) % 2 == 1;
        if (isLeftChild) parentMatch.TeamAid = winnerTeamId; else parentMatch.TeamBid = winnerTeamId;
        _uow.Repository<Match>().Update(parentMatch);
        await _uow.SaveChangesAsync();
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
            .Select(c => new ChatLogItemDto { SenderName = c.SenderAccount.FullName, Content = c.Content, SentAt = c.SentAt })
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

        return Result.Success("Đã ghi nhận phán quyết VAR.");
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

            dispute.AppealStatus = "ACCEPTED";
            dispute.AppealOutcomeNote = request.OutcomeNote;
            dispute.Status = "CLOSED";
            _uow.Repository<Dispute>().Update(dispute);
            await _uow.SaveChangesAsync();

            // Quay lại UC-32 với tỉ số mới.
            var forceUpdate = await ForceUpdateAsync(disputeId, adjudicatorAccountId, new ForceUpdateRequest
            {
                ScoreA = request.NewScoreA.Value,
                ScoreB = request.NewScoreB.Value,
                Reason = $"[Khiếu nại được chấp nhận] {request.OutcomeNote}"
            });
            if (!forceUpdate.IsSuccess) return forceUpdate;
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
