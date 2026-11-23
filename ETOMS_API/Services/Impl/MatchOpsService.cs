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
    private readonly IDisciplineService _discipline;

    public MatchOpsService(IUnitOfWork uow, IResultService resultService, IDisciplineService discipline)
    {
        _uow = uow;
        _resultService = resultService;
        _discipline = discipline;
    }

    public async Task<MatchDetailDto?> GetMatchDetailAsync(int matchId, int? viewerAccountId)
    {
        var match = await _uow.Repository<Match>().Query()
            .Include(m => m.TeamA).Include(m => m.TeamB).Include(m => m.Tournament)
            .FirstOrDefaultAsync(m => m.MatchId == matchId);
        if (match is null) return null;

        // Đội trưởng lấy từ TeamA/TeamB đã Include sẵn — không tốn thêm 2 câu SQL tới database ở xa.
        int? captainTeamId = !viewerAccountId.HasValue ? null
            : match.TeamA?.CaptainAccountId == viewerAccountId ? match.TeamAid
            : match.TeamB?.CaptainAccountId == viewerAccountId ? match.TeamBid
            : null;
        bool canManage = viewerAccountId.HasValue &&
            (match.RefereeAccountId == viewerAccountId.Value || match.Tournament.OrganizerAccountId == viewerAccountId.Value);
        bool canSeeRoom = canManage || captainTeamId is not null;

        // Đội nào đã nộp báo cáo (không đọc tỉ số), tranh chấp đang mở, tên trọng tài — gộp 1 lượt mỗi bảng.
        var reportedTeams = await _uow.Repository<MatchReport>().Query().AsNoTracking()
            .Where(r => r.MatchId == matchId).Select(r => r.TeamId).ToListAsync();
        var openDisputeId = await _uow.Repository<Dispute>().Query().AsNoTracking()
            .Where(d => d.MatchId == matchId && (d.Status == "OPEN" || d.Status == "UNDER_REVIEW"))
            .Select(d => (int?)d.DisputeId).FirstOrDefaultAsync();
        var refereeName = match.RefereeAccountId is int rid
            ? await _uow.Repository<Account>().Query().AsNoTracking().Where(a => a.AccountId == rid).Select(a => a.FullName).FirstOrDefaultAsync()
            : null;
        var myTeamMembers = captainTeamId is int myTeam
            ? await _uow.Repository<TeamMember>().Query().AsNoTracking()
                .Where(tm => tm.TeamId == myTeam && tm.Status == "ACTIVE")
                .OrderBy(tm => tm.Slot).ThenBy(tm => tm.Account.FullName)
                .Select(tm => new MatchTeamMemberOption { AccountId = tm.AccountId, FullName = tm.Account.FullName, Slot = tm.Slot })
                .ToListAsync()
            : new List<MatchTeamMemberOption>();

        return new MatchDetailDto
        {
            MyTeamMembers = myTeamMembers,
            TeamAReported = match.TeamAid.HasValue && reportedTeams.Contains(match.TeamAid.Value),
            TeamBReported = match.TeamBid.HasValue && reportedTeams.Contains(match.TeamBid.Value),
            OpenDisputeId = openDisputeId,
            IsReferee = viewerAccountId.HasValue && match.RefereeAccountId == viewerAccountId.Value,
            RefereeName = refereeName,
            ReportDeadlineAt = match.ReportDeadlineAt,
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
            MyCaptainTeamId = captainTeamId,
            CanSetRoom = canManage || (captainTeamId.HasValue && captainTeamId == match.TeamAid),
            CheckInOpenAt = match.CheckInOpenAt,
            MyTeamCheckedIn = captainTeamId.HasValue &&
                (captainTeamId == match.TeamAid ? match.TeamAcheckedInAt.HasValue : match.TeamBcheckedInAt.HasValue)
        };
    }

    // ===== MAT-04: Check-in thủ công =====
    public async Task<Result> ManualCheckInAsync(int matchId, int actorAccountId, ManualCheckInRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
            return Result.FieldFailure(nameof(request.Reason), "Điểm danh thủ công bắt buộc phải có lý do.");

        var match = await LoadForManageAsync(matchId, actorAccountId);
        if (match.Match is null) return Result.Failure("Không tìm thấy trận đấu.", StatusCodes.Status404NotFound);
        if (match.ManageError is not null) return match.ManageError;
        var m = match.Match!;

        if (m.Status != "PENDING" && m.Status != "READY")
            return Result.Failure("Trận không còn ở trạng thái chờ check-in.");
        if (await _resultService.IsFrozenByUpstreamDisputeAsync(matchId))   // UC-30 A2
            return Result.Failure(ResultService.FrozenMessage, StatusCodes.Status409Conflict);

        var side = request.TeamSide.Trim().ToUpperInvariant();
        if (side != "A" && side != "B")
            return Result.FieldFailure(nameof(request.TeamSide), "Phải chọn đội A hoặc B.");

        m.CheckInMode = "MANUAL_OVERRIDE";
        m.OverrideReason = request.Reason.Trim();
        if (side == "A") m.TeamAcheckedInAt = DateTime.UtcNow; else m.TeamBcheckedInAt = DateTime.UtcNow;
        bool lobbyOpened = false;
        if (m.TeamAcheckedInAt.HasValue && m.TeamBcheckedInAt.HasValue && m.Status == "PENDING") { m.Status = "READY"; lobbyOpened = true; }
        _uow.Repository<Match>().Update(m);

        if (lobbyOpened) await NotifyLobbyOpenAsync(m);

        await LogAsync(m.TournamentId, actorAccountId, "MANUAL_CHECKIN", m.MatchId,
            $"TeamSide={side}", request.Reason.Trim());
        await _uow.SaveChangesAsync();
        return Result.Success("Đã điểm danh thủ công.");
    }

    // UC-25 bước 1 / UC-21 bước 6: trận Sẵn sàng thì mở phòng chờ và báo cho hai đội trưởng.
    private async Task NotifyLobbyOpenAsync(Match m)
    {
        var captains = await _uow.Repository<Team>().Query()
            .Where(t => t.TeamId == m.TeamAid || t.TeamId == m.TeamBid)
            .Select(t => t.CaptainAccountId).ToListAsync();
        foreach (var cap in captains.Distinct())
            await _uow.Repository<Notification>().AddAsync(new Notification
            {
                AccountId = cap,
                NotificationType = "SYSTEM",
                Title = "Phòng chờ trận đã mở",
                Body = "Hai đội đã điểm danh. Vào phòng chờ để trao đổi mã phòng và mật khẩu với đội bạn và trọng tài.",
                LinkUrl = "/match/detail.html?id=" + m.MatchId,
                Channel = "IN_APP",
                IsRead = false,
                SentAt = DateTime.UtcNow
            });
    }

    // ===== MAT-02 / UC-21: Đội trưởng tự điểm danh =====
    // Cửa sổ điểm danh mở 30 phút trước giờ thi đấu và đóng đúng giờ thi đấu (BR-MATCH-01).
    public async Task<Result> SelfCheckInAsync(int matchId, int captainAccountId)
    {
        var ctx = await LoadLobbyAsync(matchId, captainAccountId);
        if (ctx is null) return Result.Failure("Không tìm thấy trận đấu.", StatusCodes.Status404NotFound);
        if (ctx.Role is not ("CAPTAIN_A" or "CAPTAIN_B"))
            return Result.Failure("Chỉ đội trưởng của hai đội trong trận mới điểm danh được.", StatusCodes.Status403Forbidden);
        var m = ctx.Match;
        if (m.Status is not ("PENDING" or "READY"))
            return Result.Failure("Trận không còn ở giai đoạn điểm danh.");
        if (m.ScheduledAt is null || m.CheckInOpenAt is null)
            return Result.Failure("Trận chưa được xếp giờ thi đấu nên chưa điểm danh được.");
        if (await _resultService.IsFrozenByUpstreamDisputeAsync(matchId))   // UC-30 A2
            return Result.Failure(ResultService.FrozenMessage, StatusCodes.Status409Conflict);

        var now = DateTime.UtcNow;
        if (now < m.CheckInOpenAt) // E1 — MSG-MAT-001
            return Result.Failure($"Cửa sổ điểm danh chưa mở. Mở trước giờ thi đấu 30 phút (lúc {m.CheckInOpenAt.Value.AddHours(7):HH:mm dd/MM}).");
        if (now >= m.ScheduledAt) // E2 — MSG-MAT-002
            return Result.Failure("Đã quá hạn. Vui lòng liên hệ Ban tổ chức.");

        bool isA = ctx.Role == "CAPTAIN_A";
        var already = isA ? m.TeamAcheckedInAt : m.TeamBcheckedInAt;
        if (already.HasValue)
            return Result.Failure($"Đội bạn đã điểm danh lúc {already.Value.AddHours(7):HH:mm}.");

        if (isA) m.TeamAcheckedInAt = now; else m.TeamBcheckedInAt = now;
        bool lobbyOpened = m.TeamAcheckedInAt.HasValue && m.TeamBcheckedInAt.HasValue && m.Status == "PENDING";
        if (lobbyOpened) m.Status = "READY";
        _uow.Repository<Match>().Update(m);
        if (lobbyOpened) await NotifyLobbyOpenAsync(m);
        await _uow.SaveChangesAsync();
        return Result.Success(lobbyOpened
            ? "Điểm danh thành công. Hai đội đã đủ — phòng chờ trận đã mở."
            : "Điểm danh thành công. Chờ đối thủ điểm danh."); // MSG-MAT-003
    }

    // ===== MAT-08: Phòng đấu (Room ID + mật khẩu) =====
    public async Task<Result> SetRoomInfoAsync(int matchId, int actorAccountId, SetRoomInfoRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.RoomCode))
            return Result.FieldFailure(nameof(request.RoomCode), "Vui lòng nhập mã phòng.");
        if (request.RoomCode.Trim().Length > 100)
            return Result.FieldFailure(nameof(request.RoomCode), "Mã phòng tối đa 100 ký tự.");
        if (request.RoomPassword is { } pw && pw.Trim().Length > 100)
            return Result.FieldFailure(nameof(request.RoomPassword), "Mật khẩu phòng tối đa 100 ký tự.");

        // UC-25 bước 3: đội trưởng đội nhà (đội A) chia sẻ mã phòng; trọng tài/BTC vẫn sửa được.
        var ctx = await LoadLobbyAsync(matchId, actorAccountId);
        if (ctx is null) return Result.Failure("Không tìm thấy trận đấu.", StatusCodes.Status404NotFound);
        if (ctx.Role is not ("REFEREE" or "ORGANIZER" or "CAPTAIN_A"))
            return Result.Failure("Chỉ đội trưởng đội nhà, trọng tài hoặc Ban tổ chức mới nhập được mã phòng.", StatusCodes.Status403Forbidden);
        if (ctx.Match.Status is "VERIFIED" or "WALKOVER" or "CANCELLED")
            return Result.Failure("Trận đã kết thúc, không đổi thông tin phòng được nữa.");
        var m = ctx.Match;

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

    public async Task<Result<IReadOnlyList<SubstituteRequestDto>>> GetSubstituteRequestsAsync(int matchId, int viewerAccountId)
    {
        // Chỉ trọng tài/BTC của giải hoặc thành viên 1 trong 2 đội thi đấu được xem — giống đúng
        // điều kiện của phòng chat trận (CanAccessChatAsync), vì đây cũng là thông tin nội bộ trận.
        if (!await CanAccessChatAsync(matchId, viewerAccountId))
            return Result<IReadOnlyList<SubstituteRequestDto>>.Failure("Bạn không có quyền xem yêu cầu thay người của trận này.", StatusCodes.Status403Forbidden);

        var list = await _uow.Repository<MatchRequest>().Query()
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
        return Result<IReadOnlyList<SubstituteRequestDto>>.Success(list);
    }

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

    // ===== MAT-07 / UC-25: Phòng chờ trận =====
    // Một phòng cho mỗi trận: 2 đội trưởng + trọng tài được gán + Ban tổ chức (E1). Mở khi trận Sẵn sàng (cả hai
    // đội đã điểm danh), chỉ còn đọc khi trận đã có kết quả (E2). Toàn bộ tin nhắn được lưu làm bằng chứng cho
    // người phân xử tranh chấp (UC-31), nên tin nhắn không sửa/xóa được.
    public const int ChatMaxLength = 1000;      // MatchChatMessage.Body VARCHAR(1000) trên DB chung
    private const int ChatReportNoteMax = 500;   // lý do báo cáo (gửi trong thông báo, không lưu trên tin nhắn)
    private static readonly string[] LobbyOpenStatuses = { "READY", "LIVE", "WAITING_REPORT", "DISPUTED" };

    private sealed record LobbyContext(Match Match, string? Role, int? CaptainA, int? CaptainB);

    private async Task<LobbyContext?> LoadLobbyAsync(int matchId, int accountId)
    {
        var m = await _uow.Repository<Match>().Query()
            .Include(x => x.Tournament).Include(x => x.TeamA).Include(x => x.TeamB)
            .FirstOrDefaultAsync(x => x.MatchId == matchId);
        if (m is null) return null;
        int? capA = m.TeamA?.CaptainAccountId, capB = m.TeamB?.CaptainAccountId;
        string? role = m.RefereeAccountId == accountId ? "REFEREE"
            : m.Tournament.OrganizerAccountId == accountId ? "ORGANIZER"
            : capA == accountId ? "CAPTAIN_A"
            : capB == accountId ? "CAPTAIN_B"
            : null;
        return new LobbyContext(m, role, capA, capB);
    }

    private static string? LobbyClosedReason(Match m) => m.Status switch
    {
        _ when LobbyOpenStatuses.Contains(m.Status) => null,
        "PENDING" => "Phòng chờ sẽ mở khi cả hai đội đã điểm danh (trận ở trạng thái Sẵn sàng).",
        "CANCELLED" => "Trận đã hủy — phòng chờ chỉ còn để đọc.",
        _ => "Trận đã có kết quả — phòng chờ chỉ còn để đọc."
    };

    private static string RoleLabel(string? role, Match m) => role switch
    {
        "REFEREE" => "Trọng tài",
        "ORGANIZER" => "Ban tổ chức",
        "CAPTAIN_A" => "Đội trưởng " + (m.TeamA?.Name ?? "đội A"),
        "CAPTAIN_B" => "Đội trưởng " + (m.TeamB?.Name ?? "đội B"),
        _ => "Thành viên cũ"
    };

    public async Task<Result<MatchLobbyDto>> GetChatMessagesAsync(int matchId, int viewerAccountId)
    {
        var ctx = await LoadLobbyAsync(matchId, viewerAccountId);
        if (ctx is null) return Result<MatchLobbyDto>.Failure("Không tìm thấy trận đấu.", StatusCodes.Status404NotFound);
        if (ctx.Role is null) return Result<MatchLobbyDto>.Failure("Chỉ hai đội trưởng, trọng tài và Ban tổ chức mới vào được phòng chờ trận này.", StatusCodes.Status403Forbidden);

        var m = ctx.Match;
        bool moderator = ctx.Role is "REFEREE" or "ORGANIZER";
        var rows = await _uow.Repository<MatchChatMessage>().Query().AsNoTracking()
            .Where(c => c.MatchId == matchId)
            .OrderBy(c => c.SentAt).ThenBy(c => c.MatchChatMessageId)
            .Select(c => new { c.MatchChatMessageId, c.SenderAccountId, SenderName = c.SenderAccount.FullName, c.Body, c.SentAt })
            .ToListAsync();

        var closed = LobbyClosedReason(m);
        return Result<MatchLobbyDto>.Success(new MatchLobbyDto
        {
            IsOpen = closed is null,
            StateMessage = closed ?? "Phòng chờ đang mở. Tin nhắn được lưu lại làm bằng chứng nếu trận có tranh chấp.",
            MyRoleLabel = RoleLabel(ctx.Role, m),
            MaxLength = ChatMaxLength,
            Messages = rows.Select(c =>
            {
                var senderRole = c.SenderAccountId == m.RefereeAccountId ? "REFEREE"
                    : c.SenderAccountId == m.Tournament.OrganizerAccountId ? "ORGANIZER"
                    : c.SenderAccountId == ctx.CaptainA ? "CAPTAIN_A"
                    : c.SenderAccountId == ctx.CaptainB ? "CAPTAIN_B" : "FORMER";
                bool mine = c.SenderAccountId == viewerAccountId;
                return new ChatMessageDto
                {
                    MatchChatMessageId = c.MatchChatMessageId,
                    SenderAccountId = c.SenderAccountId,
                    SenderName = c.SenderName,
                    Content = c.Body,
                    SentAt = c.SentAt,
                    SenderRole = senderRole,
                    SenderRoleLabel = RoleLabel(senderRole, m),
                    IsMine = mine,
                    // Bảng tin nhắn trên DB chung không còn cột báo cáo: báo cáo chỉ gửi thông báo cho BTC/trọng tài.
                    ReportStatus = "NONE",
                    CanReport = !mine && !moderator
                };
            }).ToList()
        });
    }

    public async Task<Result<ChatMessageDto>> SendChatMessageAsync(int matchId, int senderAccountId, SendChatMessageRequest request)
    {
        var content = (request.Content ?? string.Empty).Trim();
        if (content.Length == 0)
            return Result<ChatMessageDto>.FieldFailure(nameof(request.Content), "Vui lòng nhập nội dung tin nhắn.");
        if (content.Length > ChatMaxLength)
            return Result<ChatMessageDto>.FieldFailure(nameof(request.Content), $"Tin nhắn tối đa {ChatMaxLength} ký tự.");

        var ctx = await LoadLobbyAsync(matchId, senderAccountId);
        if (ctx is null) return Result<ChatMessageDto>.Failure("Không tìm thấy trận đấu.", StatusCodes.Status404NotFound);
        if (ctx.Role is null) return Result<ChatMessageDto>.Failure("Chỉ hai đội trưởng, trọng tài và Ban tổ chức mới gửi được tin trong phòng chờ.", StatusCodes.Status403Forbidden);
        var closed = LobbyClosedReason(ctx.Match);
        if (closed is not null) return Result<ChatMessageDto>.Failure(closed);

        // Chống gửi dồn: tối đa 5 tin trong 10 giây cho mỗi người.
        var since = DateTime.UtcNow.AddSeconds(-10);
        if (await _uow.Repository<MatchChatMessage>().Query().CountAsync(c => c.MatchId == matchId && c.SenderAccountId == senderAccountId && c.SentAt >= since) >= 5)
            return Result<ChatMessageDto>.Failure("Bạn gửi quá nhanh, vui lòng đợi vài giây rồi gửi tiếp.", StatusCodes.Status429TooManyRequests);

        var msg = new MatchChatMessage
        {
            MatchId = matchId,
            SenderAccountId = senderAccountId,
            // CK_MatchChatMessage_Role: CAPTAIN / ORGANIZER / REFEREE; TeamID chỉ có với đội trưởng.
            SenderRole = ctx.Role is "CAPTAIN_A" or "CAPTAIN_B" ? "CAPTAIN" : ctx.Role!,
            TeamId = ctx.Role == "CAPTAIN_A" ? ctx.Match.TeamAid : ctx.Role == "CAPTAIN_B" ? ctx.Match.TeamBid : null,
            Body = content,
            SentAt = DateTime.UtcNow
        };
        await _uow.Repository<MatchChatMessage>().AddAsync(msg);
        await _uow.SaveChangesAsync();
        return Result<ChatMessageDto>.Success(new ChatMessageDto
        {
            MatchChatMessageId = msg.MatchChatMessageId,
            SenderAccountId = senderAccountId,
            Content = msg.Body,
            SentAt = msg.SentAt,
            SenderRole = ctx.Role,
            SenderRoleLabel = RoleLabel(ctx.Role, ctx.Match),
            IsMine = true
        }, "Đã gửi tin nhắn.");
    }

    // UC-25 A2: báo cáo tin nhắn xúc phạm — gửi thông báo cho Ban tổ chức + trọng tài (có thể phạt qua UC-34).
    // Bảng MatchChatMessage trên DB chung không có cột báo cáo nên không đánh dấu lên tin nhắn; tin nhắn vẫn
    // nằm nguyên trong log làm bằng chứng.
    public async Task<Result> ReportChatMessageAsync(int matchId, int messageId, int reporterAccountId, ReportChatMessageRequest request)
    {
        var note = (request.Note ?? string.Empty).Trim();
        if (note.Length < 5 || note.Length > ChatReportNoteMax)
            return Result.FieldFailure(nameof(request.Note), $"Vui lòng ghi lý do báo cáo (5–{ChatReportNoteMax} ký tự).");

        var ctx = await LoadLobbyAsync(matchId, reporterAccountId);
        if (ctx is null) return Result.Failure("Không tìm thấy trận đấu.", StatusCodes.Status404NotFound);
        if (ctx.Role is null) return Result.Failure("Chỉ người trong phòng chờ mới báo cáo được tin nhắn.", StatusCodes.Status403Forbidden);

        var msg = await _uow.Repository<MatchChatMessage>().Query().AsNoTracking()
            .FirstOrDefaultAsync(c => c.MatchChatMessageId == messageId && c.MatchId == matchId);
        if (msg is null) return Result.Failure("Không tìm thấy tin nhắn trong phòng chờ này.", StatusCodes.Status404NotFound);
        if (msg.SenderAccountId == reporterAccountId) return Result.Failure("Không thể báo cáo tin nhắn của chính bạn.");

        var m = ctx.Match;
        var preview = msg.Body.Length > 120 ? msg.Body[..120] + "…" : msg.Body;
        foreach (var to in new[] { m.Tournament.OrganizerAccountId, m.RefereeAccountId ?? 0 }.Where(x => x > 0 && x != reporterAccountId).Distinct())
        {
            await _uow.Repository<Notification>().AddAsync(new Notification
            {
                AccountId = to,
                NotificationType = "SYSTEM",
                Title = "Tin nhắn bị báo cáo trong phòng chờ",
                Body = $"Trận {m.TeamA?.Name ?? "đội A"} vs {m.TeamB?.Name ?? "đội B"}: tin nhắn \"{preview}\" bị báo cáo. Lý do: {note}",
                LinkUrl = "/match/detail.html?id=" + matchId,
                Channel = "IN_APP",
                IsRead = false,
                SentAt = DateTime.UtcNow
            });
        }
        await _uow.SaveChangesAsync();
        return Result.Success("Đã gửi báo cáo tới Ban tổ chức.");
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

        // UC-26 "Other Information": đảo kết quả trong MỘT giao dịch — reset dở dang để nhánh ở trạng thái không luật nào mô tả.
        await using var tx = await _uow.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);

        // A1/A2: gỡ đội đã được đẩy sang trận sau; trận sau đã đá thì cũng hủy để đấu lại.
        var downstream = await _resultService.RevertAdvancementAsync(matchId, apply: true);

        var oldReports = await _uow.Repository<MatchReport>().Query().Where(r => r.MatchId == matchId).ToListAsync();
        foreach (var r in oldReports) _uow.Repository<MatchReport>().Remove(r);

        m.ScoreA = null; m.ScoreB = null; m.WinnerTeamId = null;
        m.ResultSource = null; m.VerifiedAt = null;
        m.TeamAcheckedInAt = null; m.TeamBcheckedInAt = null;
        m.CheckInMode = "SELF"; m.OverrideReason = null;
        m.Status = "PENDING";
        _uow.Repository<Match>().Update(m);

        var reason = request.Reason.Trim();
        await LogAsync(m.TournamentId, actorAccountId, "REMATCH", m.MatchId,
            "Status=PENDING" + (downstream.Count > 0 ? "; AlsoReset=" + string.Join(",", downstream.Select(d => d.MatchId)) : ""), reason);
        foreach (var d in downstream)
            await LogAsync(d.TournamentId, actorAccountId, "REMATCH", d.MatchId, $"Status=PENDING; Cause=REMATCH_OF_{matchId}", reason);

        // Bước 2: báo hai đội trưởng của trận; A2: báo các đội có trận đã đá phía sau sẽ phải đấu lại.
        var teamIds = new[] { m.TeamAid, m.TeamBid }.Concat(downstream.SelectMany(d => new[] { d.TeamAid, d.TeamBid }))
            .Where(x => x.HasValue).Select(x => x!.Value).Distinct().ToList();
        var captains = await _uow.Repository<Team>().Query().Where(t => teamIds.Contains(t.TeamId))
            .Select(t => new { t.TeamId, t.CaptainAccountId }).ToListAsync();
        foreach (var c in captains)
        {
            var own = c.TeamId == m.TeamAid || c.TeamId == m.TeamBid;
            await _uow.Repository<Notification>().AddAsync(new Notification
            {
                AccountId = c.CaptainAccountId, NotificationType = "MATCH_RESULT",
                Title = own ? "Trận đấu được cho đấu lại" : "Trận đã đá của đội bạn sẽ đấu lại",
                Body = (own ? $"Kết quả trận #{matchId} đã bị hủy, trận sẽ được xếp giờ và đấu lại." :
                        $"Trận #{matchId} ở vòng trước được cho đấu lại nên kết quả trận đội bạn đã đá phía sau cũng bị hủy và sẽ đấu lại.")
                    + " Lý do: " + (reason.Length > 600 ? reason[..600] : reason),
                LinkUrl = "/match/detail.html?id=" + (own ? matchId : (downstream.FirstOrDefault(d => d.TeamAid == c.TeamId || d.TeamBid == c.TeamId)?.MatchId ?? matchId)),
                Channel = "IN_APP", IsRead = false, SentAt = DateTime.UtcNow
            });
        }
        await _uow.SaveChangesAsync();
        await tx.CommitAsync();
        return Result.Success(downstream.Count > 0
            ? $"Đã hủy kết quả cũ và {downstream.Count} trận đã đá phía sau; các trận quay lại trạng thái chờ đấu."
            : "Đã hủy kết quả cũ, trận quay lại trạng thái chờ đấu.");
    }

    // UC-26 bước 3 / A2: xem trước các trận phía sau sẽ bị hủy kết quả nếu cho trận này đấu lại.
    public async Task<Result<List<RematchImpactDto>>> PreviewRematchAsync(int matchId, int actorAccountId)
    {
        var match = await LoadForManageAsync(matchId, actorAccountId);
        if (match.Match is null) return Result.Failure<List<RematchImpactDto>>("Không tìm thấy trận đấu.", StatusCodes.Status404NotFound);
        if (match.ManageError is not null) return Result.Failure<List<RematchImpactDto>>(match.ManageError.Message, match.ManageError.StatusCode);
        var list = await _resultService.RevertAdvancementAsync(matchId, apply: false);
        var ids = list.SelectMany(d => new[] { d.TeamAid, d.TeamBid }).Where(x => x.HasValue).Select(x => x!.Value).Distinct().ToList();
        var names = await _uow.Repository<Team>().Query().Where(t => ids.Contains(t.TeamId)).ToDictionaryAsync(t => t.TeamId, t => t.Name);
        return Result.Success(list.Select(d => new RematchImpactDto
        {
            MatchId = d.MatchId,
            Label = $"{(d.TeamAid is int a ? names.GetValueOrDefault(a) : "?")} vs {(d.TeamBid is int b ? names.GetValueOrDefault(b) : "?")}",
            Status = StatusText.Match(d.Status)
        }).ToList());
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

        await LogAsync(m.TournamentId, actorAccountId, "WALKOVER", m.MatchId,
            $"WinnerTeamId={winnerTeamId}; DisqualifiedTeamId={request.TeamId}", request.Reason.Trim());
        await _uow.SaveChangesAsync();

        if (winnerTeamId.HasValue) await _resultService.AdvanceWinnerAsync(matchId, winnerTeamId.Value);

        // UC-24 bước 2: báo đội trưởng đội được xử thắng (đội bị tước quyền được báo trong DisqualifyTeamAsync).
        if (winnerTeamId.HasValue)
        {
            var winnerCaptain = await _uow.Repository<Team>().Query().Where(t => t.TeamId == winnerTeamId.Value)
                .Select(t => t.CaptainAccountId).FirstAsync();
            await _uow.Repository<Notification>().AddAsync(new Notification
            {
                AccountId = winnerCaptain,
                NotificationType = "PENALTY",
                Title = "Đội bạn thắng trắng",
                Body = $"Đối thủ trận #{matchId} đã bị tước quyền thi đấu; đội bạn được xử thắng trắng.",
                LinkUrl = "/match/detail.html?id=" + matchId,
                Channel = "IN_APP",
                IsRead = false,
                SentAt = DateTime.UtcNow
            });
        }

        // UC-24 hậu điều kiện: đội rời giải (đơn → DISQUALIFIED), MỌI trận còn lại xử thắng trắng cho đối thủ,
        // một dòng nhật ký DISQUALIFY ghi người thao tác + lý do, báo đội trưởng hai đội (OBS-02 đã sửa).
        var resolved = await _discipline.DisqualifyTeamAsync(m.TournamentId, request.TeamId, actorAccountId, request.Reason.Trim(),
            $"Cause=MANUAL; FromMatch={matchId}", matchId);
        var others = resolved - 1;
        return Result.Success(others > 0
            ? $"Đã tước quyền thi đấu của đội và xử thắng trắng thêm {others} trận còn lại."
            : "Đã tước quyền thi đấu của đội.");
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

    // ===== NUI-MAT-02: Tự đánh vắng và xử thắng trắng (Scheduler) =====
    // Hết cửa sổ điểm danh (đúng giờ thi đấu, BR-MATCH-01) mà chỉ một đội điểm danh: đội còn lại vắng mặt, trận WALKOVER cho
    // đội có mặt (cùng engine UC-23), ghi nhật ký người thực hiện = hệ thống. Hai đội cùng vắng thì để BTC xếp lại giờ (UC-23 A1).
    // BTC muốn hủy quyết định tự động thì dùng "Hủy & đấu lại" (UC-26) kèm lý do.
    public async Task<int> ProcessNoShowsAsync(DateTime nowUtc, CancellationToken ct = default)
    {
        var due = await _uow.Repository<Match>().Query()
            .Include(m => m.TeamA).Include(m => m.TeamB).Include(m => m.Tournament)
            .Where(m => m.Status == "PENDING" && m.ScheduledAt != null && m.ScheduledAt <= nowUtc
                && m.TeamAid != null && m.TeamBid != null && m.Tournament.Status == "ONGOING"
                && ((m.TeamAcheckedInAt != null && m.TeamBcheckedInAt == null) || (m.TeamAcheckedInAt == null && m.TeamBcheckedInAt != null)))
            .OrderBy(m => m.MatchId)
            .ToListAsync(ct);
        var done = 0;
        foreach (var m in due)
        {
            if (await _resultService.IsFrozenByUpstreamDisputeAsync(m.MatchId)) continue;   // UC-30 A2: chờ phán quyết trận trước
            var winner = m.TeamAcheckedInAt != null ? m.TeamAid!.Value : m.TeamBid!.Value;
            var absent = winner == m.TeamAid ? m.TeamB! : m.TeamA!;
            var present = winner == m.TeamAid ? m.TeamA! : m.TeamB!;
            m.Status = "WALKOVER";
            m.ResultSource = "WALKOVER";
            m.WinnerTeamId = winner;
            await LogAsync(m.TournamentId, null, "WALKOVER", m.MatchId,
                $"WinnerTeamId={winner}; NoShowTeamId={absent.TeamId}; Cause=NO_SHOW",
                $"Tự động: đội {absent.Name} không điểm danh trước giờ thi đấu {m.ScheduledAt!.Value.AddHours(7):HH:mm dd/MM/yyyy}.");
            foreach (var (account, title, body) in new[]
            {
                (absent.CaptainAccountId, "Đội bạn bị xử thua vì vắng mặt", $"Đội {absent.Name} không điểm danh trước giờ thi đấu trận #{m.MatchId} nên bị xử thua trắng. Liên hệ Ban tổ chức nếu có sai sót."),
                (present.CaptainAccountId, "Đội bạn thắng trắng", $"Đối thủ {absent.Name} không điểm danh trước giờ thi đấu trận #{m.MatchId}; đội bạn được xử thắng trắng."),
                (m.Tournament.OrganizerAccountId, "Trận được xử thắng trắng tự động", $"Trận #{m.MatchId}: {absent.Name} không điểm danh, {present.Name} thắng trắng. Có thể hủy và cho đấu lại kèm lý do nếu cần.")
            })
                await _uow.Repository<Notification>().AddAsync(new Notification
                {
                    AccountId = account, NotificationType = "MATCH_RESULT", Title = title, Body = body,
                    LinkUrl = "/match/detail.html?id=" + m.MatchId, Channel = "IN_APP", IsRead = false, SentAt = DateTime.UtcNow
                });
            await _uow.SaveChangesAsync();
            await _resultService.AdvanceWinnerAsync(m.MatchId, winner);
            done++;
        }
        return done;
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
