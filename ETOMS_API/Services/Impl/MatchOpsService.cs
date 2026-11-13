using ETOMS_API.DTOs.Match;
using ETOMS_API.Models;
using ETOMS_API.Repositories.Interface;
using ETOMS_API.Services.Interface;
using ETOMS_API.Utils;
using Microsoft.EntityFrameworkCore;

namespace ETOMS_API.Services.Impl;

// STUB TẠM thay module MAT (Vũ Đức Hải: MAT-01/04/05/06/07, Nguyễn Quang Anh: MAT-08..12) —
// đủ để demo luồng vào trận. Dùng chung IResultService.AdvanceWinnerAsync/GetCaptainTeamIdAsync
// để trận kết thúc do walkover/disqualify/forfeit cũng đẩy đội thắng lên vòng trên như RES.
public class MatchOpsService : IMatchOpsService
{
    private readonly IUnitOfWork _uow;
    private readonly IResultService _resultService;

    public MatchOpsService(IUnitOfWork uow, IResultService resultService)
    {
        _uow = uow;
        _resultService = resultService;
    }

    public async Task<MatchDetailDto?> GetMatchDetailAsync(int matchId, int? viewerAccountId)
    {
        var match = await _uow.Repository<Match>().Query()
            .Include(m => m.TeamA).Include(m => m.TeamB).Include(m => m.Tournament)
            .FirstOrDefaultAsync(m => m.MatchId == matchId);
        if (match is null) return null;

        int? captainTeamId = viewerAccountId.HasValue
            ? await _resultService.GetCaptainTeamIdAsync(matchId, viewerAccountId.Value)
            : null;
        bool canManage = viewerAccountId.HasValue &&
            (match.RefereeAccountId == viewerAccountId.Value || match.Tournament.OrganizerAccountId == viewerAccountId.Value);
        bool canSeeRoom = canManage || captainTeamId is not null;

        return new MatchDetailDto
        {
            MatchId = match.MatchId,
            TournamentId = match.TournamentId,
            TeamAId = match.TeamAid,
            TeamAName = match.TeamA?.Name ?? "Chưa xác định",
            TeamBId = match.TeamBid,
            TeamBName = match.TeamB?.Name ?? "Chưa xác định",
            BestOf = match.BestOf,
            ScheduledAt = match.ScheduledAt,
            Status = match.Status,
            ScoreA = match.ScoreA,
            ScoreB = match.ScoreB,
            WinnerTeamId = match.WinnerTeamId,
            RoomCode = canSeeRoom ? match.RoomCode : null,
            RoomPassword = canSeeRoom ? match.RoomPassword : null,
            TeamACheckedIn = match.TeamAcheckedInAt.HasValue,
            TeamBCheckedIn = match.TeamBcheckedInAt.HasValue,
            CanManage = canManage,
            MyCaptainTeamId = captainTeamId
        };
    }

    // ===== MAT-04: Check-in thủ công =====
    public async Task<Result> ManualCheckInAsync(int matchId, int actorAccountId, ManualCheckInRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
            return Result.FieldFailure(nameof(request.Reason), "Check-in thủ công bắt buộc phải có lý do.");

        var match = await LoadForManageAsync(matchId, actorAccountId);
        if (match.Match is null) return Result.Failure("Không tìm thấy trận đấu.", StatusCodes.Status404NotFound);
        if (match.ManageError is not null) return match.ManageError;
        var m = match.Match!;

        if (m.Status != "PENDING" && m.Status != "READY")
            return Result.Failure("Trận không còn ở trạng thái chờ check-in.");

        var side = request.TeamSide.Trim().ToUpperInvariant();
        if (side != "A" && side != "B")
            return Result.FieldFailure(nameof(request.TeamSide), "Phải chọn đội A hoặc B.");

        m.CheckInMode = "MANUAL_OVERRIDE";
        m.OverrideReason = request.Reason.Trim();
        if (side == "A") m.TeamAcheckedInAt = DateTime.UtcNow; else m.TeamBcheckedInAt = DateTime.UtcNow;
        if (m.TeamAcheckedInAt.HasValue && m.TeamBcheckedInAt.HasValue && m.Status == "PENDING") m.Status = "READY";
        _uow.Repository<Match>().Update(m);

        await LogAsync(m.TournamentId, actorAccountId, "MANUAL_CHECKIN", m.MatchId,
            $"TeamSide={side}", request.Reason.Trim());
        await _uow.SaveChangesAsync();
        return Result.Success("Đã check-in thủ công.");
    }

    // ===== MAT-08: Phòng đấu (Room ID + mật khẩu) =====
    public async Task<Result> SetRoomInfoAsync(int matchId, int actorAccountId, SetRoomInfoRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.RoomCode))
            return Result.FieldFailure(nameof(request.RoomCode), "Vui lòng nhập mã phòng.");

        var match = await LoadForManageAsync(matchId, actorAccountId);
        if (match.Match is null) return Result.Failure("Không tìm thấy trận đấu.", StatusCodes.Status404NotFound);
        if (match.ManageError is not null) return match.ManageError;
        var m = match.Match!;

        m.RoomCode = request.RoomCode.Trim();
        m.RoomPassword = string.IsNullOrWhiteSpace(request.RoomPassword) ? null : request.RoomPassword.Trim();
        _uow.Repository<Match>().Update(m);
        await _uow.SaveChangesAsync();
        return Result.Success("Đã cập nhật thông tin phòng đấu.");
    }

    // ===== MAT-05: Yêu cầu thay người dự bị =====
    public async Task<Result<int>> RequestSubstituteAsync(int matchId, int requestedByAccountId, CreateSubstituteRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
            return Result<int>.FieldFailure(nameof(request.Reason), "Vui lòng nêu lý do thay người.");
        if (request.OutAccountId == request.InAccountId)
            return Result<int>.FieldFailure(nameof(request.InAccountId), "Người vào phải khác người ra.");

        var match = await _uow.Repository<Match>().GetByIdAsync(matchId);
        if (match is null) return Result<int>.Failure("Không tìm thấy trận đấu.", StatusCodes.Status404NotFound);
        if (match.TeamAid != request.TeamId && match.TeamBid != request.TeamId)
            return Result<int>.FieldFailure(nameof(request.TeamId), "Đội không thuộc trận này.");

        var captainTeamId = await _resultService.GetCaptainTeamIdAsync(matchId, requestedByAccountId);
        if (captainTeamId != request.TeamId)
            return Result<int>.Failure("Chỉ đội trưởng của đội trong trận này mới được xin thay người.", StatusCodes.Status403Forbidden);

        bool outIsMember = await _uow.Repository<TeamMember>().AnyAsync(x =>
            x.TeamId == request.TeamId && x.AccountId == request.OutAccountId && x.Status == "ACTIVE");
        if (!outIsMember) return Result<int>.FieldFailure(nameof(request.OutAccountId), "Người ra không thuộc đội.");

        bool inIsMember = await _uow.Repository<TeamMember>().AnyAsync(x =>
            x.TeamId == request.TeamId && x.AccountId == request.InAccountId && x.Status == "ACTIVE");
        if (!inIsMember) return Result<int>.FieldFailure(nameof(request.InAccountId), "Người vào không thuộc đội.");

        var mr = new MatchRequest
        {
            MatchId = matchId,
            TeamId = request.TeamId,
            RequestKind = "SUBSTITUTION",
            OutAccountId = request.OutAccountId,
            InAccountId = request.InAccountId,
            Reason = request.Reason.Trim(),
            Status = "PENDING",
            RequestedByAccountId = requestedByAccountId,
            CreatedAt = DateTime.UtcNow
        };
        await _uow.Repository<MatchRequest>().AddAsync(mr);
        await _uow.SaveChangesAsync();
        return Result<int>.Success(mr.MatchRequestId, "Đã gửi yêu cầu thay người, chờ trọng tài/BTC duyệt.");
    }

    public async Task<IReadOnlyList<SubstituteRequestDto>> GetSubstituteRequestsAsync(int matchId)
        => await _uow.Repository<MatchRequest>().Query()
            .Include(r => r.OutAccount).Include(r => r.InAccount)
            .Where(r => r.MatchId == matchId && r.RequestKind == "SUBSTITUTION")
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new SubstituteRequestDto
            {
                MatchRequestId = r.MatchRequestId,
                TeamId = r.TeamId ?? 0,
                OutAccountId = r.OutAccountId ?? 0,
                OutName = r.OutAccount != null ? r.OutAccount.FullName : "",
                InAccountId = r.InAccountId ?? 0,
                InName = r.InAccount != null ? r.InAccount.FullName : "",
                Reason = r.Reason,
                Status = r.Status,
                CreatedAt = r.CreatedAt
            }).ToListAsync();

    // ===== MAT-06: Duyệt thay người =====
    public async Task<Result> DecideSubstituteAsync(int matchRequestId, int decidedByAccountId, bool approve)
    {
        var mr = await _uow.Repository<MatchRequest>().GetByIdAsync(matchRequestId);
        if (mr is null) return Result.Failure("Không tìm thấy yêu cầu.", StatusCodes.Status404NotFound);
        if (mr.Status != "PENDING") return Result.Failure("Yêu cầu đã được xử lý.");

        var match = await LoadForManageAsync(mr.MatchId, decidedByAccountId);
        if (match.Match is null) return Result.Failure("Không tìm thấy trận đấu.", StatusCodes.Status404NotFound);
        if (match.ManageError is not null) return match.ManageError;

        mr.Status = approve ? "APPROVED" : "REJECTED";
        mr.DecidedByAccountId = decidedByAccountId;
        mr.DecidedAt = DateTime.UtcNow;
        _uow.Repository<MatchRequest>().Update(mr);
        await _uow.SaveChangesAsync();
        return Result.Success(approve ? "Đã duyệt thay người." : "Đã từ chối yêu cầu thay người.");
    }

    // ===== MAT-07: Phòng chờ chat =====
    public async Task<Result<IReadOnlyList<ChatMessageDto>>> GetChatMessagesAsync(int matchId, int viewerAccountId)
    {
        var access = await CanAccessChatAsync(matchId, viewerAccountId);
        if (!access) return Result<IReadOnlyList<ChatMessageDto>>.Failure("Bạn không có quyền xem phòng chờ trận này.", StatusCodes.Status403Forbidden);

        var list = await _uow.Repository<MatchChatMessage>().Query()
            .Include(c => c.SenderAccount)
            .Where(c => c.MatchId == matchId)
            .OrderBy(c => c.SentAt)
            .Select(c => new ChatMessageDto
            {
                MatchChatMessageId = c.MatchChatMessageId,
                SenderAccountId = c.SenderAccountId,
                SenderName = c.SenderAccount.FullName,
                Content = c.Content,
                SentAt = c.SentAt
            }).ToListAsync();
        return Result<IReadOnlyList<ChatMessageDto>>.Success(list);
    }

    public async Task<Result<ChatMessageDto>> SendChatMessageAsync(int matchId, int senderAccountId, SendChatMessageRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
            return Result<ChatMessageDto>.FieldFailure(nameof(request.Content), "Vui lòng nhập nội dung.");
        if (!await CanAccessChatAsync(matchId, senderAccountId))
            return Result<ChatMessageDto>.Failure("Bạn không có quyền gửi vào phòng chờ trận này.", StatusCodes.Status403Forbidden);

        var sender = await _uow.Repository<Account>().GetByIdAsync(senderAccountId);
        var msg = new MatchChatMessage
        {
            MatchId = matchId,
            SenderAccountId = senderAccountId,
            Content = request.Content.Trim(),
            IsPinned = false,
            ReportStatus = "NONE",
            SentAt = DateTime.UtcNow
        };
        await _uow.Repository<MatchChatMessage>().AddAsync(msg);
        await _uow.SaveChangesAsync();

        return Result<ChatMessageDto>.Success(new ChatMessageDto
        {
            MatchChatMessageId = msg.MatchChatMessageId,
            SenderAccountId = senderAccountId,
            SenderName = sender?.FullName ?? "",
            Content = msg.Content,
            SentAt = msg.SentAt
        });
    }

    private async Task<bool> CanAccessChatAsync(int matchId, int accountId)
    {
        var match = await _uow.Repository<Match>().Query()
            .Include(m => m.Tournament).FirstOrDefaultAsync(m => m.MatchId == matchId);
        if (match is null) return false;
        if (match.RefereeAccountId == accountId || match.Tournament.OrganizerAccountId == accountId) return true;

        bool isMember = (match.TeamAid.HasValue && await _uow.Repository<TeamMember>().AnyAsync(x =>
                x.TeamId == match.TeamAid.Value && x.AccountId == accountId && x.Status == "ACTIVE"))
            || (match.TeamBid.HasValue && await _uow.Repository<TeamMember>().AnyAsync(x =>
                x.TeamId == match.TeamBid.Value && x.AccountId == accountId && x.Status == "ACTIVE"));
        return isMember;
    }

    // ===== MAT-09: Thắng trắng (Walkover) =====
    public async Task<Result> ProcessWalkoverAsync(int matchId, int actorAccountId, WalkoverRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
            return Result.FieldFailure(nameof(request.Reason), "Vui lòng nêu lý do xử thắng trắng.");

        var match = await LoadForManageAsync(matchId, actorAccountId);
        if (match.Match is null) return Result.Failure("Không tìm thấy trận đấu.", StatusCodes.Status404NotFound);
        if (match.ManageError is not null) return match.ManageError;
        var m = match.Match!;

        if (m.Status is "VERIFIED" or "CANCELLED" or "WALKOVER")
            return Result.Failure("Trận đã kết thúc, không thể xử thắng trắng.");
        if (request.WinnerTeamId != m.TeamAid && request.WinnerTeamId != m.TeamBid)
            return Result.FieldFailure(nameof(request.WinnerTeamId), "Đội thắng không thuộc trận này.");

        m.Status = "WALKOVER";
        m.ResultSource = "WALKOVER";
        m.WinnerTeamId = request.WinnerTeamId;
        _uow.Repository<Match>().Update(m);
        await LogAsync(m.TournamentId, actorAccountId, "WALKOVER", m.MatchId,
            $"WinnerTeamId={request.WinnerTeamId}", request.Reason.Trim());
        await _uow.SaveChangesAsync();

        await _resultService.AdvanceWinnerAsync(matchId, request.WinnerTeamId);
        return Result.Success("Đã xử thắng trắng.");
    }

    // ===== MAT-10: Hủy & đấu lại (Rematch) =====
    public async Task<Result> RematchAsync(int matchId, int actorAccountId, RematchRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
            return Result.FieldFailure(nameof(request.Reason), "Vui lòng nêu lý do đấu lại.");

        var match = await LoadForManageAsync(matchId, actorAccountId);
        if (match.Match is null) return Result.Failure("Không tìm thấy trận đấu.", StatusCodes.Status404NotFound);
        if (match.ManageError is not null) return match.ManageError;
        var m = match.Match!;

        var oldReports = await _uow.Repository<MatchReport>().Query().Where(r => r.MatchId == matchId).ToListAsync();
        foreach (var r in oldReports) _uow.Repository<MatchReport>().Remove(r);

        m.ScoreA = null; m.ScoreB = null; m.WinnerTeamId = null;
        m.ResultSource = null; m.VerifiedAt = null;
        m.TeamAcheckedInAt = null; m.TeamBcheckedInAt = null;
        m.CheckInMode = "SELF"; m.OverrideReason = null;
        m.Status = "PENDING";
        _uow.Repository<Match>().Update(m);

        await LogAsync(m.TournamentId, actorAccountId, "REMATCH", m.MatchId, "Status=PENDING", request.Reason.Trim());
        await _uow.SaveChangesAsync();
        return Result.Success("Đã hủy kết quả cũ, trận quay lại trạng thái chờ đấu.");
    }

    // ===== MAT-11: Tước quyền thi đấu (Disqualify) =====
    public async Task<Result> DisqualifyAsync(int matchId, int actorAccountId, DisqualifyRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
            return Result.FieldFailure(nameof(request.Reason), "Vui lòng nêu lý do tước quyền thi đấu.");

        var match = await LoadForManageAsync(matchId, actorAccountId);
        if (match.Match is null) return Result.Failure("Không tìm thấy trận đấu.", StatusCodes.Status404NotFound);
        if (match.ManageError is not null) return match.ManageError;
        var m = match.Match!;

        if (request.TeamId != m.TeamAid && request.TeamId != m.TeamBid)
            return Result.FieldFailure(nameof(request.TeamId), "Đội không thuộc trận này.");
        if (m.Status is "VERIFIED" or "CANCELLED" or "WALKOVER")
            return Result.Failure("Trận đã kết thúc, không thể tước quyền thi đấu.");

        var winnerTeamId = request.TeamId == m.TeamAid ? m.TeamBid : m.TeamAid;

        await _uow.Repository<Penalty>().AddAsync(new Penalty
        {
            TournamentId = m.TournamentId,
            TeamId = request.TeamId,
            MatchId = matchId,
            Severity = "DISQUALIFICATION",
            Points = 0,
            Reason = request.Reason.Trim(),
            IssuedByAccountId = actorAccountId,
            Status = "ACTIVE",
            IssuedAt = DateTime.UtcNow
        });

        m.Status = "WALKOVER";
        m.ResultSource = "WALKOVER";
        m.WinnerTeamId = winnerTeamId;
        _uow.Repository<Match>().Update(m);

        await LogAsync(m.TournamentId, actorAccountId, "DISQUALIFY", m.MatchId,
            $"DisqualifiedTeamId={request.TeamId}", request.Reason.Trim());
        await _uow.SaveChangesAsync();

        if (winnerTeamId.HasValue) await _resultService.AdvanceWinnerAsync(matchId, winnerTeamId.Value);
        return Result.Success("Đã tước quyền thi đấu của đội.");
    }

    // ===== MAT-12: Bỏ cuộc giữa giải (Forfeit) — đội trưởng tự khai =====
    public async Task<Result> ForfeitAsync(int matchId, int captainAccountId, ForfeitRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
            return Result.FieldFailure(nameof(request.Reason), "Vui lòng nêu lý do bỏ cuộc.");

        var captainTeamId = await _resultService.GetCaptainTeamIdAsync(matchId, captainAccountId);
        if (captainTeamId is null || captainTeamId != request.TeamId)
            return Result.Failure("Chỉ đội trưởng của đội trong trận này mới được xin bỏ cuộc.", StatusCodes.Status403Forbidden);

        var m = await _uow.Repository<Match>().GetByIdAsync(matchId);
        if (m is null) return Result.Failure("Không tìm thấy trận đấu.", StatusCodes.Status404NotFound);
        if (m.Status is "VERIFIED" or "CANCELLED" or "WALKOVER")
            return Result.Failure("Trận đã kết thúc, không thể bỏ cuộc.");

        var winnerTeamId = request.TeamId == m.TeamAid ? m.TeamBid : m.TeamAid;
        m.Status = "WALKOVER";
        m.ResultSource = "WALKOVER";
        m.WinnerTeamId = winnerTeamId;
        _uow.Repository<Match>().Update(m);

        await LogAsync(m.TournamentId, captainAccountId, "WALKOVER", m.MatchId,
            $"ForfeitByTeamId={request.TeamId}", "Đội trưởng xin bỏ cuộc: " + request.Reason.Trim());
        await _uow.SaveChangesAsync();

        if (winnerTeamId.HasValue) await _resultService.AdvanceWinnerAsync(matchId, winnerTeamId.Value);
        return Result.Success("Đã ghi nhận đội bỏ cuộc.");
    }

    // ===== Helpers =====
    private async Task<(Match? Match, Result? ManageError)> LoadForManageAsync(int matchId, int actorAccountId)
    {
        var m = await _uow.Repository<Match>().Query()
            .Include(x => x.Tournament)
            .FirstOrDefaultAsync(x => x.MatchId == matchId);
        if (m is null) return (null, null);
        if (m.RefereeAccountId != actorAccountId && m.Tournament.OrganizerAccountId != actorAccountId)
            return (m, Result.Failure("Chỉ trọng tài của trận hoặc BTC mới được thao tác.", StatusCodes.Status403Forbidden));
        return (m, null);
    }

    private Task LogAsync(int tournamentId, int? actorAccountId, string actionType, int matchId, string afterValue, string reason)
        => _uow.Repository<AuditLog>().AddAsync(new AuditLog
        {
            ActorAccountId = actorAccountId,
            TournamentId = tournamentId,
            ActionType = actionType,
            ObjectType = "MATCH",
            ObjectId = matchId,
            AfterValue = afterValue,
            Reason = reason
        });
}
