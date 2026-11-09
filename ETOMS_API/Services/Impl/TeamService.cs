using ETOMS_API.Utils;
using ETOMS_API.DTOs.Team;
using ETOMS_API.Repositories;
using ETOMS_API.Repositories.Interface;
using ETOMS_API.Services.Interface;
using ETOMS_API.Models;
using Microsoft.EntityFrameworkCore;

namespace ETOMS_API.Services.Impl;

// Module TEAM (v2 schema). Đội trưởng = Team.CaptainAccountId và cũng là một TeamMember
// (Slot = STARTER). Thành viên lưu ở TeamMember với Slot STARTER/SUBSTITUTE, không có vai trò
// CAPTAIN trong Slot. Mã mời là MỘT mã nằm trên Team.
public class TeamService : ITeamService
{
    private readonly IUnitOfWork _uow;

    public TeamService(IUnitOfWork uow) => _uow = uow;

    // ===== TEAM-01: Tạo đội =====
    public async Task<Result<int>> CreateTeamAsync(CreateTeamRequest request, int captainAccountId)
    {
        var name = request.Name.Trim();
        var tag = request.Tag.Trim();

        var gameOk = await _uow.Repository<Game>()
            .AnyAsync(g => g.GameId == request.GameId && g.Status == "ACTIVE");
        if (!gameOk)
            return Result<int>.FieldFailure(nameof(request.GameId), "Tựa game không hợp lệ hoặc đã ngừng hoạt động.");

        if (await _uow.Repository<Team>().AnyAsync(t => t.GameId == request.GameId && t.Name == name))
            return Result<int>.FieldFailure(nameof(request.Name), "Tên đội đã tồn tại trong tựa game này, vui lòng chọn tên khác.");

        if (await _uow.Repository<Team>().AnyAsync(t => t.GameId == request.GameId && t.Tag == tag))
            return Result<int>.FieldFailure(nameof(request.Tag), "Thẻ đội đã tồn tại trong tựa game này, vui lòng chọn thẻ khác.");

        // Người tạo không được đang thuộc một đội khác cùng tựa game (DB cũng ép).
        if (await _uow.Repository<TeamMember>().AnyAsync(m =>
                m.GameId == request.GameId && m.AccountId == captainAccountId && m.Status == "ACTIVE"))
            return Result<int>.FieldFailure(nameof(request.GameId), "Bạn đã thuộc một đội trong tựa game này, không thể tạo thêm đội.");

        var team = new Team
        {
            GameId = request.GameId,
            CaptainAccountId = captainAccountId,
            Name = name,
            Tag = tag,
            LogoUrl = request.LogoUrl,
            Status = "ACTIVE",
            CreatedAt = DateTime.UtcNow
        };
        await _uow.Repository<Team>().AddAsync(team);
        await _uow.SaveChangesAsync();

        // Đội trưởng là một thành viên chính thức.
        await _uow.Repository<TeamMember>().AddAsync(new TeamMember
        {
            TeamId = team.TeamId,
            GameId = team.GameId,
            AccountId = captainAccountId,
            Slot = "STARTER",
            Direction = "JOIN_BY_CODE",
            Status = "ACTIVE",
            JoinedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        });
        await _uow.SaveChangesAsync();

        return Result<int>.Success(team.TeamId, "Tạo đội thành công.");
    }

    public async Task<IReadOnlyList<TeamListItemDto>> GetAllTeamsAsync(TeamSearchRequest? filter = null)
    {
        filter ??= new TeamSearchRequest();
        var query = _uow.Repository<Team>().Query();

        if (!string.IsNullOrWhiteSpace(filter.Q))
        {
            var q = filter.Q.Trim();
            query = query.Where(t => t.Name.Contains(q) || t.Tag.Contains(q));
        }
        if (filter.GameId.HasValue)
            query = query.Where(t => t.GameId == filter.GameId.Value);
        if (!string.IsNullOrWhiteSpace(filter.Status) && filter.Status != "ALL")
            query = query.Where(t => t.Status == filter.Status);

        query = filter.Sort switch
        {
            "name" => query.OrderBy(t => t.Name),
            "members" => query.OrderByDescending(t => t.TeamMembers.Count(m => m.Status == "ACTIVE")),
            _ => query.OrderByDescending(t => t.CreatedAt)
        };

        return await query
            .Select(t => new TeamListItemDto
            {
                TeamId = t.TeamId,
                Name = t.Name,
                Tag = t.Tag,
                LogoUrl = t.LogoUrl,
                GameTitle = t.Game.Title,
                CaptainName = t.CaptainAccount.FullName,
                MemberCount = t.TeamMembers.Count(m => m.Status == "ACTIVE"),
                Status = t.Status
            })
            .ToListAsync();
    }

    public async Task<TeamDetailDto?> GetTeamDetailAsync(int teamId)
    {
        var dto = await _uow.Repository<Team>().Query()
            .Where(t => t.TeamId == teamId)
            .Select(t => new TeamDetailDto
            {
                TeamId = t.TeamId,
                CaptainAccountId = t.CaptainAccountId,
                Name = t.Name,
                Tag = t.Tag,
                LogoUrl = t.LogoUrl,
                GameTitle = t.Game.Title,
                CaptainName = t.CaptainAccount.FullName,
                Status = t.Status,
                CreatedAt = t.CreatedAt
            })
            .FirstOrDefaultAsync();
        if (dto is null) return null;

        dto.Members = await LoadMembersAsync(teamId, dto.CaptainAccountId);

        // TEAM-09: tỉ lệ thắng/thua — chỉ tính trận đã VERIFIED (có kết quả chính thức, LI-01).
        var verifiedMatches = await _uow.Repository<Match>().Query()
            .Where(m => m.Status == "VERIFIED" && (m.TeamAid == teamId || m.TeamBid == teamId))
            .Select(m => m.WinnerTeamId)
            .ToListAsync();
        dto.Wins = verifiedMatches.Count(w => w == teamId);
        dto.Losses = verifiedMatches.Count(w => w != null && w != teamId);

        // TEAM-09: danh hiệu + điểm xếp hạng, gộp mọi mùa (vw_Leaderboard).
        var leaderboardRows = await _uow.Repository<VwLeaderboard>().Query()
            .Where(v => v.TeamId == teamId).ToListAsync();
        dto.TitleCount = (int)leaderboardRows.Sum(v => v.TitleCount ?? 0);
        dto.RankingPoint = (int)leaderboardRows.Sum(v => v.AchievementPoint ?? 0);

        // TEAM-09: lịch sử giải đã tham gia (đơn đã duyệt trở đi, không tính đơn bị từ chối/rút).
        dto.TournamentHistory = await _uow.Repository<Registration>().Query()
            .Where(r => r.TeamId == teamId && r.Status != "PENDING" && r.Status != "REJECTED" && r.Status != "WITHDRAWN")
            .OrderByDescending(r => r.SubmittedAt)
            .Select(r => new TeamTournamentHistoryDto
            {
                TournamentId = r.TournamentId,
                TournamentName = r.Tournament.Name,
                TournamentStatus = r.Tournament.Status,
                FinalPlacement = r.FinalPlacement,
                FinalRank = r.FinalRank
            })
            .ToListAsync();

        return dto;
    }

    public async Task<TeamDashboardDto?> GetDashboardAsync(int teamId)
    {
        var dto = await _uow.Repository<Team>().Query()
            .Where(t => t.TeamId == teamId)
            .Select(t => new TeamDashboardDto
            {
                TeamId = t.TeamId,
                CaptainAccountId = t.CaptainAccountId,
                Name = t.Name,
                Tag = t.Tag,
                LogoUrl = t.LogoUrl,
                GameTitle = t.Game.Title,
                Status = t.Status,
                MaxStarters = t.Game.MaxStarters,
                MaxSubstitutes = t.Game.MaxSubstitutes,
                InviteCode = t.InviteCode,
                InviteExpiresAt = t.InviteExpiresAt
            })
            .FirstOrDefaultAsync();
        if (dto is null) return null;

        dto.Members = await LoadMembersAsync(teamId, dto.CaptainAccountId);
        dto.ActiveMemberCount = dto.Members.Count;
        dto.StarterCount = dto.Members.Count(m => m.Slot == "STARTER");
        dto.SubCount = dto.Members.Count(m => m.Slot == "SUBSTITUTE");

        dto.TournamentCount = await _uow.Repository<Registration>().Query()
            .CountAsync(r => r.TeamId == teamId
                && r.Status != "DRAFT" && r.Status != "REJECTED" && r.Status != "WITHDRAWN" && r.Status != "DISQUALIFIED");
        dto.PenaltyCount = await _uow.Repository<Penalty>().Query()
            .CountAsync(p => p.TeamId == teamId && p.Status == "ACTIVE");
        dto.PendingCount = await _uow.Repository<TeamMember>().Query()
            .CountAsync(m => m.TeamId == teamId && m.Status == "PENDING");

        return dto;
    }

    public async Task<int?> GetCaptainAccountIdAsync(int teamId)
        => await _uow.Repository<Team>().Query()
            .Where(t => t.TeamId == teamId).Select(t => (int?)t.CaptainAccountId).FirstOrDefaultAsync();

    // ===== TEAM-04: Mã mời (1 mã trên Team) =====
    public async Task<InviteInfoDto> GetInviteAsync(int teamId)
    {
        return await _uow.Repository<Team>().Query()
            .Where(t => t.TeamId == teamId)
            .Select(t => new InviteInfoDto { TeamId = t.TeamId, Code = t.InviteCode, ExpiresAt = t.InviteExpiresAt })
            .FirstAsync();
    }

    public async Task<Result<string>> GenerateInviteAsync(int teamId, int captainAccountId, CreateInviteRequest request)
    {
        var team = await _uow.Repository<Team>().GetByIdAsync(teamId);
        if (team is null) return Result<string>.Failure("Không tìm thấy đội.");
        if (team.CaptainAccountId != captainAccountId) return Result<string>.Failure("Chỉ đội trưởng được tạo mã mời.");

        string code;
        do { code = GenerateCode(); }
        while (await _uow.Repository<Team>().AnyAsync(t => t.InviteCode == code));

        team.InviteCode = code;
        team.InviteExpiresAt = request.ExpiresHours.HasValue ? DateTime.UtcNow.AddHours(request.ExpiresHours.Value) : null;
        _uow.Repository<Team>().Update(team);
        await _uow.SaveChangesAsync();
        return Result<string>.Success(code, "Đã tạo mã mời mới.");
    }

    public async Task<Result> RevokeInviteAsync(int teamId, int captainAccountId)
    {
        var team = await _uow.Repository<Team>().GetByIdAsync(teamId);
        if (team is null) return Result.Failure("Không tìm thấy đội.");
        if (team.CaptainAccountId != captainAccountId) return Result.Failure("Chỉ đội trưởng được thu hồi mã.");

        team.InviteCode = null;
        team.InviteExpiresAt = null;
        _uow.Repository<Team>().Update(team);
        await _uow.SaveChangesAsync();
        return Result.Success("Đã thu hồi mã mời.");
    }

    // ===== TEAM-05: Gia nhập bằng mã =====
    public async Task<Result<int>> JoinByCodeAsync(string code, int accountId)
    {
        code = code.Trim().ToUpperInvariant();

        var team = await _uow.Repository<Team>().Query().FirstOrDefaultAsync(t => t.InviteCode == code);
        if (team is null)
            return Result<int>.FieldFailure(nameof(JoinTeamRequest.Code), "Mã mời không tồn tại hoặc đã bị thu hồi.");
        if (team.Status != "ACTIVE")
            return Result<int>.FieldFailure(nameof(JoinTeamRequest.Code), "Đội không còn hoạt động.");
        if (team.InviteExpiresAt.HasValue && team.InviteExpiresAt.Value <= DateTime.UtcNow)
            return Result<int>.FieldFailure(nameof(JoinTeamRequest.Code), "Mã mời đã hết hạn.");

        if (await _uow.Repository<TeamMember>().AnyAsync(m => m.TeamId == team.TeamId && m.AccountId == accountId && m.Status == "ACTIVE"))
            return Result<int>.FieldFailure(nameof(JoinTeamRequest.Code), "Bạn đã ở trong đội này.");
        if (await _uow.Repository<TeamMember>().AnyAsync(m => m.GameId == team.GameId && m.AccountId == accountId && m.Status == "ACTIVE"))
            return Result<int>.FieldFailure(nameof(JoinTeamRequest.Code), "Bạn đã thuộc một đội khác trong tựa game này.");

        var game = await _uow.Repository<Game>().GetByIdAsync(team.GameId);
        var (starters, subs) = await CountSlotsAsync(team.TeamId);
        string slot;
        if (game is not null && starters < game.MaxStarters) slot = "STARTER";
        else if (game is not null && subs < game.MaxSubstitutes) slot = "SUBSTITUTE";
        else return Result<int>.FieldFailure(nameof(JoinTeamRequest.Code), "Đội đã đủ quân số theo luật, không thể nhận thêm.");

        await _uow.Repository<TeamMember>().AddAsync(new TeamMember
        {
            TeamId = team.TeamId,
            GameId = team.GameId,
            AccountId = accountId,
            Slot = slot,
            Direction = "JOIN_BY_CODE",
            Status = "ACTIVE",
            JoinedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        });
        try { await _uow.SaveChangesAsync(); }
        catch (DbUpdateException)
        {
            return Result<int>.FieldFailure(nameof(JoinTeamRequest.Code), "Không thể gia nhập: bạn đã thuộc một đội trong tựa game này hoặc đội vừa đầy.");
        }
        return Result<int>.Success(team.TeamId, "Gia nhập đội thành công.");
    }

    // ===== TEAM-03: Roster =====
    public async Task<RosterDto?> GetRosterAsync(int teamId)
    {
        var dto = await _uow.Repository<Team>().Query()
            .Where(t => t.TeamId == teamId)
            .Select(t => new RosterDto
            {
                TeamId = t.TeamId,
                CaptainAccountId = t.CaptainAccountId,
                Name = t.Name,
                Tag = t.Tag,
                GameTitle = t.Game.Title,
                MaxStarters = t.Game.MaxStarters,
                MaxSubstitutes = t.Game.MaxSubstitutes
            })
            .FirstOrDefaultAsync();
        if (dto is null) return null;

        dto.IsLocked = await IsRosterLockedAsync(teamId);
        dto.Members = await LoadMembersAsync(teamId, dto.CaptainAccountId);
        dto.PendingMembers = await _uow.Repository<TeamMember>().Query()
            .Where(m => m.TeamId == teamId && m.Status == "PENDING")
            .OrderBy(m => m.CreatedAt)
            .Select(m => new TeamMemberDto
            {
                AccountId = m.AccountId,
                FullName = m.Account.FullName,
                AvatarUrl = m.Account.AvatarUrl,
                Slot = m.Slot,
                Status = m.Status,
                Direction = m.Direction,
                Message = m.Message
            })
            .ToListAsync();
        return dto;
    }

    public async Task<Result> ChangeSlotAsync(int teamId, int targetAccountId, string newSlot, int captainAccountId)
    {
        if (newSlot != "STARTER" && newSlot != "SUBSTITUTE") return Result.Failure("Vị trí không hợp lệ.");
        var team = await _uow.Repository<Team>().GetByIdAsync(teamId);
        if (team is null) return Result.Failure("Không tìm thấy đội.");
        if (team.CaptainAccountId != captainAccountId) return Result.Failure("Chỉ đội trưởng được thao tác.");
        if (await IsRosterLockedAsync(teamId)) return Result.Failure("Roster đang bị khóa do đội đang thi đấu.");
        if (targetAccountId == team.CaptainAccountId) return Result.Failure("Không thể đổi vị trí của đội trưởng.");

        var m = await _uow.Repository<TeamMember>().Query()
            .FirstOrDefaultAsync(x => x.TeamId == teamId && x.AccountId == targetAccountId && x.Status == "ACTIVE");
        if (m is null) return Result.Failure("Thành viên không thuộc đội.");
        if (m.Slot == newSlot) return Result.Success();

        var game = await _uow.Repository<Game>().GetByIdAsync(team.GameId);
        var (starters, subs) = await CountSlotsAsync(teamId);
        if (newSlot == "STARTER" && game is not null && starters >= game.MaxStarters)
            return Result.Failure($"Đã đủ {game.MaxStarters} suất chính thức theo luật.");
        if (newSlot == "SUBSTITUTE" && game is not null && subs >= game.MaxSubstitutes)
            return Result.Failure($"Đã đủ {game.MaxSubstitutes} suất dự bị theo luật.");

        m.Slot = newSlot;
        _uow.Repository<TeamMember>().Update(m);
        await _uow.SaveChangesAsync();
        return Result.Success("Đã cập nhật vị trí.");
    }

    public async Task<Result> KickMemberAsync(int teamId, int targetAccountId, int captainAccountId)
    {
        var team = await _uow.Repository<Team>().GetByIdAsync(teamId);
        if (team is null) return Result.Failure("Không tìm thấy đội.");
        if (team.CaptainAccountId != captainAccountId) return Result.Failure("Chỉ đội trưởng được thao tác.");
        if (await IsRosterLockedAsync(teamId)) return Result.Failure("Roster đang bị khóa do đội đang thi đấu.");
        if (targetAccountId == team.CaptainAccountId) return Result.Failure("Không thể loại đội trưởng khỏi đội.");

        var m = await _uow.Repository<TeamMember>().Query()
            .FirstOrDefaultAsync(x => x.TeamId == teamId && x.AccountId == targetAccountId && x.Status == "ACTIVE");
        if (m is null) return Result.Failure("Thành viên không thuộc đội.");

        m.Status = "REMOVED"; m.LeftAt = DateTime.UtcNow;
        _uow.Repository<TeamMember>().Update(m);
        await _uow.SaveChangesAsync();
        return Result.Success("Đã loại thành viên khỏi đội.");
    }

    public async Task<Result> ApproveMemberAsync(int teamId, int targetAccountId, int captainAccountId)
    {
        var team = await _uow.Repository<Team>().GetByIdAsync(teamId);
        if (team is null) return Result.Failure("Không tìm thấy đội.");
        if (team.CaptainAccountId != captainAccountId) return Result.Failure("Chỉ đội trưởng được duyệt đơn.");
        if (await IsRosterLockedAsync(teamId)) return Result.Failure("Roster đang bị khóa do đội đang thi đấu.");

        var m = await _uow.Repository<TeamMember>().Query()
            .FirstOrDefaultAsync(x => x.TeamId == teamId && x.AccountId == targetAccountId && x.Status == "PENDING");
        if (m is null) return Result.Failure("Không tìm thấy đơn chờ duyệt.");

        var game = await _uow.Repository<Game>().GetByIdAsync(team.GameId);
        var (starters, subs) = await CountSlotsAsync(teamId);
        if (game is not null && starters < game.MaxStarters) m.Slot = "STARTER";
        else if (game is not null && subs < game.MaxSubstitutes) m.Slot = "SUBSTITUTE";
        else return Result.Failure("Đội đã đủ quân số theo luật, không thể duyệt thêm.");

        m.Status = "ACTIVE";
        m.JoinedAt = DateTime.UtcNow;
        _uow.Repository<TeamMember>().Update(m);
        try { await _uow.SaveChangesAsync(); }
        catch (DbUpdateException)
        {
            return Result.Failure("Không thể duyệt: người này đã thuộc một đội khác trong tựa game.");
        }
        return Result.Success("Đã duyệt đơn, thành viên gia nhập đội.");
    }

    public async Task<Result> RejectMemberAsync(int teamId, int targetAccountId, int captainAccountId)
    {
        var team = await _uow.Repository<Team>().GetByIdAsync(teamId);
        if (team is null) return Result.Failure("Không tìm thấy đội.");
        if (team.CaptainAccountId != captainAccountId) return Result.Failure("Chỉ đội trưởng được từ chối đơn.");

        var m = await _uow.Repository<TeamMember>().Query()
            .FirstOrDefaultAsync(x => x.TeamId == teamId && x.AccountId == targetAccountId && x.Status == "PENDING");
        if (m is null) return Result.Failure("Không tìm thấy đơn chờ duyệt.");

        m.Status = "REJECTED";
        _uow.Repository<TeamMember>().Update(m);
        await _uow.SaveChangesAsync();
        return Result.Success("Đã từ chối đơn.");
    }

    // ===== TEAM-07: Chuyển quyền đội trưởng =====
    public async Task<Result> TransferCaptainAsync(int teamId, int newCaptainAccountId, int currentCaptainAccountId)
    {
        var team = await _uow.Repository<Team>().GetByIdAsync(teamId);
        if (team is null) return Result.Failure("Không tìm thấy đội.");
        if (team.CaptainAccountId != currentCaptainAccountId) return Result.Failure("Chỉ đội trưởng hiện tại được chuyển quyền.");
        if (newCaptainAccountId == currentCaptainAccountId) return Result.Failure("Người nhận phải khác đội trưởng hiện tại.");

        var newCap = await _uow.Repository<TeamMember>().Query()
            .FirstOrDefaultAsync(m => m.TeamId == teamId && m.AccountId == newCaptainAccountId && m.Status == "ACTIVE");
        if (newCap is null) return Result.Failure("Người nhận phải là thành viên đang hoạt động của đội.");

        // Đội trưởng mới nên là chính thức.
        if (newCap.Slot != "STARTER") { newCap.Slot = "STARTER"; _uow.Repository<TeamMember>().Update(newCap); }
        team.CaptainAccountId = newCaptainAccountId;
        _uow.Repository<Team>().Update(team);
        await _uow.SaveChangesAsync();
        return Result.Success("Đã chuyển quyền đội trưởng.");
    }

    // ===== TEAM-06: Rời đội =====
    public async Task<Result> LeaveTeamAsync(int teamId, int accountId)
    {
        var team = await _uow.Repository<Team>().GetByIdAsync(teamId);
        if (team is null) return Result.Failure("Không tìm thấy đội.");
        if (team.CaptainAccountId == accountId)
            return Result.Failure("Đội trưởng phải chuyển quyền hoặc giải tán đội trước khi rời.");
        if (await IsRosterLockedAsync(teamId))
            return Result.Failure("Đội đang thi đấu, roster bị khóa. Vui lòng liên hệ BTC/Trọng tài.");

        var m = await _uow.Repository<TeamMember>().Query()
            .FirstOrDefaultAsync(x => x.TeamId == teamId && x.AccountId == accountId && x.Status == "ACTIVE");
        if (m is null) return Result.Failure("Bạn không thuộc đội này.");

        m.Status = "LEFT"; m.LeftAt = DateTime.UtcNow;
        _uow.Repository<TeamMember>().Update(m);
        await _uow.SaveChangesAsync();
        return Result.Success("Bạn đã rời đội.");
    }

    // ===== TEAM-08: Giải tán đội =====
    public async Task<Result> DisbandTeamAsync(int teamId, int captainAccountId)
    {
        var team = await _uow.Repository<Team>().GetByIdAsync(teamId);
        if (team is null) return Result.Failure("Không tìm thấy đội.");
        if (team.CaptainAccountId != captainAccountId) return Result.Failure("Chỉ đội trưởng được giải tán đội.");
        if (team.Status == "DISBANDED") return Result.Failure("Đội đã được giải tán.");
        if (await IsInUnfinishedTournamentAsync(teamId))
            return Result.Failure("Không thể giải tán: đội đang tham gia một giải chưa kết thúc.");

        team.Status = "DISBANDED";
        team.InviteCode = null; team.InviteExpiresAt = null;
        _uow.Repository<Team>().Update(team);

        var actives = await _uow.Repository<TeamMember>().Query()
            .Where(m => m.TeamId == teamId && m.Status == "ACTIVE").ToListAsync();
        foreach (var m in actives) { m.Status = "LEFT"; m.LeftAt = DateTime.UtcNow; _uow.Repository<TeamMember>().Update(m); }

        await _uow.SaveChangesAsync();
        return Result.Success("Đã giải tán đội. Lịch sử thi đấu được giữ lại.");
    }

    public async Task<bool> IsActiveMemberAsync(int teamId, int accountId)
        => await _uow.Repository<TeamMember>().AnyAsync(m => m.TeamId == teamId && m.AccountId == accountId && m.Status == "ACTIVE");

    public async Task<IReadOnlyList<GameOptionDto>> GetActiveGamesAsync()
        => await _uow.Repository<Game>().Query()
            .Where(g => g.Status == "ACTIVE").OrderBy(g => g.Title)
            .Select(g => new GameOptionDto { GameId = g.GameId, Title = g.Title })
            .ToListAsync();

    // ===== Helpers =====
    private async Task<List<TeamMemberDto>> LoadMembersAsync(int teamId, int captainAccountId)
    {
        var members = await _uow.Repository<TeamMember>().Query()
            .Where(m => m.TeamId == teamId && m.Status == "ACTIVE")
            .Select(m => new TeamMemberDto
            {
                AccountId = m.AccountId,
                FullName = m.Account.FullName,
                AvatarUrl = m.Account.AvatarUrl,
                Slot = m.Slot,
                Status = m.Status
            })
            .ToListAsync();
        foreach (var m in members) m.IsCaptain = m.AccountId == captainAccountId;
        return members
            .OrderByDescending(m => m.IsCaptain)
            .ThenBy(m => m.Slot == "STARTER" ? 0 : 1)
            .ThenBy(m => m.FullName)
            .ToList();
    }

    private async Task<(int starters, int subs)> CountSlotsAsync(int teamId)
    {
        var slots = await _uow.Repository<TeamMember>().Query()
            .Where(m => m.TeamId == teamId && m.Status == "ACTIVE").Select(m => m.Slot).ToListAsync();
        return (slots.Count(s => s == "STARTER"), slots.Count(s => s == "SUBSTITUTE"));
    }

    private Task<bool> IsRosterLockedAsync(int teamId)
        => _uow.Repository<Registration>().AnyAsync(r =>
            r.TeamId == teamId && r.Status == "APPROVED" && r.Tournament.Status == "ONGOING");

    private Task<bool> IsInUnfinishedTournamentAsync(int teamId)
        => _uow.Repository<Registration>().AnyAsync(r =>
            r.TeamId == teamId
            && (r.Status == "PENDING" || r.Status == "APPROVED" || r.Status == "WAITLISTED")
            && r.Tournament.Status != "COMPLETED" && r.Tournament.Status != "CANCELLED");

    private static string GenerateCode()
    {
        const string pool = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var chars = new char[8];
        for (int i = 0; i < chars.Length; i++) chars[i] = pool[Random.Shared.Next(pool.Length)];
        return new string(chars);
    }
}
