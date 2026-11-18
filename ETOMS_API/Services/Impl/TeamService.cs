using ETOMS_API.Utils;
using ETOMS_API.DTOs.Team;
using ETOMS_API.Repositories;
using ETOMS_API.Repositories.Interface;
using ETOMS_API.Services.Interface;
using ETOMS_API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace ETOMS_API.Services.Impl;

// Đội trưởng = Team.CaptainAccountId, đồng thời là 1 TeamMember Slot=STARTER (không có
// Slot riêng cho CAPTAIN). Mã mời là một mã duy nhất nằm trên Team.
public class TeamService : ITeamService
{
    private readonly IUnitOfWork _uow;

    private readonly Microsoft.Extensions.Caching.Memory.IMemoryCache _cache;

    public TeamService(IUnitOfWork uow, Microsoft.Extensions.Caching.Memory.IMemoryCache cache)
    {
        _uow = uow;
        _cache = cache;
    }

    // ===== TEAM-01: Tạo đội =====
    public async Task<Result<int>> CreateTeamAsync(CreateTeamRequest request, int captainAccountId)
    {
        var name = request.Name.Trim();
        var tag = request.Tag.Trim();

        // PermissionMatrix TEAM-01: chỉ tuyển thủ (trở thành đội trưởng) được tạo đội — BTC/Admin không.
        var creator = await _uow.Repository<Account>().GetByIdAsync(captainAccountId);
        if (creator is null || creator.Role != "PLAYER")
            return Result<int>.Failure("Chỉ tài khoản tuyển thủ được tạo đội. Ban tổ chức và quản trị viên không tham gia thi đấu.", StatusCodes.Status403Forbidden);

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

        var description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        if (description is { Length: > 500 })
            return Result<int>.FieldFailure(nameof(request.Description), "Giới thiệu đội tối đa 500 ký tự.");

        // Mời tuyển thủ ngay khi tạo đội (không bắt buộc) — kiểm hết TRƯỚC khi tạo để không sinh đội dở dang.
        var game = await _uow.Repository<Game>().GetByIdAsync(request.GameId);
        var inviteIds = (request.InviteAccountIds ?? new List<int>()).Where(id => id > 0).Distinct().ToList();
        var maxInvites = Math.Max(0, (game?.MaxStarters ?? 1) + (game?.MaxSubstitutes ?? 0) - 1);
        if (inviteIds.Contains(captainAccountId))
            return Result<int>.FieldFailure(nameof(request.InviteAccountIds), "Không cần mời chính mình — bạn là đội trưởng.");
        if (inviteIds.Count > maxInvites)
            return Result<int>.FieldFailure(nameof(request.InviteAccountIds), $"Tựa game này mỗi đội tối đa {maxInvites + 1} người, nên chỉ mời được tối đa {maxInvites} tuyển thủ.");
        var invitees = inviteIds.Count == 0 ? new List<Account>() : await _uow.Repository<Account>().Query()
            .Where(a => inviteIds.Contains(a.AccountId) && a.Status == "ACTIVE" && a.Role == "PLAYER").ToListAsync();
        if (invitees.Count != inviteIds.Count)
            return Result<int>.FieldFailure(nameof(request.InviteAccountIds), "Có tuyển thủ được mời không tồn tại hoặc đang bị khóa.");
        var busy = inviteIds.Count == 0 ? new List<int>() : await _uow.Repository<TeamMember>().Query()
            .Where(m => m.GameId == request.GameId && m.Status == "ACTIVE" && inviteIds.Contains(m.AccountId))
            .Select(m => m.AccountId).Distinct().ToListAsync();
        if (busy.Count > 0)
            return Result<int>.FieldFailure(nameof(request.InviteAccountIds),
                "Đã thuộc một đội của tựa game này nên không mời được: " + string.Join(", ", invitees.Where(a => busy.Contains(a.AccountId)).Select(a => a.FullName)) + ".");

        var now = DateTime.UtcNow;
        var team = new Team
        {
            GameId = request.GameId,
            CaptainAccountId = captainAccountId,
            Name = name,
            Tag = tag,
            LogoUrl = request.LogoUrl,
            Description = description,
            Status = "ACTIVE",
            CreatedAt = now
        };
        await _uow.Repository<Team>().AddAsync(team);

        // Đội trưởng là một thành viên chính thức. Gắn qua navigation để đội + thành viên + lời mời lưu trong MỘT lần.
        await _uow.Repository<TeamMember>().AddAsync(new TeamMember
        {
            Team = team,
            GameId = request.GameId,
            AccountId = captainAccountId,
            Slot = "STARTER",
            Direction = "JOIN_BY_CODE",
            Status = "ACTIVE",
            JoinedAt = now,
            CreatedAt = now
        });
        foreach (var invitee in invitees)
        {
            // Lời mời chờ tuyển thủ chấp nhận; vị trí chính thức/dự bị xếp lại lúc chấp nhận.
            await _uow.Repository<TeamMember>().AddAsync(new TeamMember
            {
                Team = team,
                GameId = request.GameId,
                AccountId = invitee.AccountId,
                Slot = "STARTER",
                Direction = "INVITED",
                Status = "PENDING",
                Message = "Mời tham gia ngay khi lập đội.",
                CreatedAt = now
            });
            await _uow.Repository<Notification>().AddAsync(new Notification
            {
                AccountId = invitee.AccountId,
                NotificationType = "SYSTEM",
                Title = "Lời mời vào đội " + name,
                Body = $"{creator.FullName} mời bạn tham gia đội {name} [{tag}] ({game?.Title}). Vào \"Các đội của tôi\" để chấp nhận hoặc từ chối.",
                LinkUrl = "/team/mine.html#invitations",
                Channel = "IN_APP",
                IsRead = false,
                SentAt = now
            });
        }
        await _uow.SaveChangesAsync();

        return Result<int>.Success(team.TeamId, invitees.Count == 0
            ? "Tạo đội thành công."
            : $"Tạo đội thành công. Đã gửi lời mời tới {invitees.Count} tuyển thủ.");
    }

    // ===== TEAM-01: tìm tuyển thủ để mời ngay khi tạo đội =====
    public async Task<Result<IReadOnlyList<PlayerSearchItemDto>>> SearchPlayersAsync(string? query, int gameId, int requesterAccountId)
    {
        var q = (query ?? string.Empty).Trim();
        if (q.Length < 2 || q.Length > 50)
            return Result<IReadOnlyList<PlayerSearchItemDto>>.FieldFailure("q", "Nhập từ 2 đến 50 ký tự để tìm tuyển thủ.");
        if (!await _uow.Repository<Game>().AnyAsync(g => g.GameId == gameId && g.Status == "ACTIVE"))
            return Result<IReadOnlyList<PlayerSearchItemDto>>.FieldFailure("gameId", "Hãy chọn tựa game trước khi tìm tuyển thủ.");

        // Tìm theo họ tên hoặc tên trong game của tựa game đang chọn. Chỉ tuyển thủ đang hoạt động.
        var list = await _uow.Repository<Account>().Query()
            .Where(a => a.Status == "ACTIVE" && a.Role == "PLAYER" && a.AccountId != requesterAccountId
                && (a.FullName.Contains(q) || a.PlayerCredentials.Any(c => c.GameId == gameId && c.CredentialType == "IN_GAME_NAME" && c.Value != null && c.Value.Contains(q))))
            .OrderBy(a => a.FullName)
            .Take(10)
            .Select(a => new PlayerSearchItemDto
            {
                AccountId = a.AccountId,
                FullName = a.FullName,
                AvatarUrl = a.AvatarUrl,
                InGameName = a.PlayerCredentials.Where(c => c.GameId == gameId && c.CredentialType == "IN_GAME_NAME").Select(c => c.Value).FirstOrDefault(),
                AlreadyInTeam = a.TeamMembers.Any(m => m.GameId == gameId && m.Status == "ACTIVE")
            })
            .ToListAsync();
        return Result<IReadOnlyList<PlayerSearchItemDto>>.Success(list);
    }

    // ===== Lời mời vào đội phía tuyển thủ (TeamMember INVITED + PENDING) =====
    public async Task<IReadOnlyList<MyTeamInvitationDto>> GetMyInvitationsAsync(int accountId)
        => await _uow.Repository<TeamMember>().Query()
            .Where(m => m.AccountId == accountId && m.Direction == "INVITED" && m.Status == "PENDING" && m.Team.Status == "ACTIVE")
            .OrderByDescending(m => m.CreatedAt)
            .Select(m => new MyTeamInvitationDto
            {
                TeamMemberId = m.TeamMemberId,
                TeamId = m.TeamId,
                TeamName = m.Team.Name,
                TeamTag = m.Team.Tag,
                LogoUrl = m.Team.LogoUrl,
                GameTitle = m.Team.Game.Title,
                CaptainName = m.Team.CaptainAccount.FullName,
                Message = m.Message,
                InvitedAt = m.CreatedAt
            })
            .ToListAsync();

    public async Task<Result> RespondInvitationAsync(int teamMemberId, int accountId, bool accept)
    {
        var m = await _uow.Repository<TeamMember>().Query()
            .Include(x => x.Team)
            .FirstOrDefaultAsync(x => x.TeamMemberId == teamMemberId && x.AccountId == accountId
                && x.Direction == "INVITED" && x.Status == "PENDING");
        if (m is null) return Result.Failure("Không tìm thấy lời mời hoặc lời mời đã được xử lý.", StatusCodes.Status404NotFound);

        var me = await _uow.Repository<Account>().GetByIdAsync(accountId);
        string Who() => me?.FullName ?? "Tuyển thủ";
        if (!accept)
        {
            m.Status = "REJECTED";
            _uow.Repository<TeamMember>().Update(m);
            await NotifyCaptainAsync(m.Team, $"{Who()} đã từ chối lời mời vào đội {m.Team.Name}.");
            await _uow.SaveChangesAsync();
            return Result.Success("Đã từ chối lời mời.");
        }

        if (m.Team.Status != "ACTIVE") return Result.Failure("Đội đã giải tán, lời mời không còn hiệu lực.");
        if (me is null || me.Status != "ACTIVE") return Result.Failure("Tài khoản của bạn đang không hoạt động.");
        if (me.Role != "PLAYER") return Result.Failure(NotPlayerMessage, StatusCodes.Status403Forbidden);
        if (await IsRosterLockedAsync(m.TeamId)) return Result.Failure("Đội đang thi đấu nên tạm khóa đội hình, hãy chấp nhận sau khi giải kết thúc.");
        if (await _uow.Repository<TeamMember>().AnyAsync(x => x.AccountId == accountId && x.GameId == m.GameId && x.Status == "ACTIVE"))
            return Result.Failure("Bạn đã thuộc một đội khác của tựa game này. Rời đội đó trước khi nhận lời mời.");

        var game = await _uow.Repository<Game>().GetByIdAsync(m.GameId);
        var (starters, subs) = await CountSlotsAsync(m.TeamId);
        if (game is not null && starters < game.MaxStarters) m.Slot = "STARTER";
        else if (game is not null && subs < game.MaxSubstitutes) m.Slot = "SUBSTITUTE";
        else return Result.Failure("Đội đã đủ quân số theo luật của tựa game.");

        m.Status = "ACTIVE";
        m.JoinedAt = DateTime.UtcNow;
        _uow.Repository<TeamMember>().Update(m);
        await NotifyCaptainAsync(m.Team, $"{Who()} đã nhận lời mời và gia nhập đội {m.Team.Name}.");
        try { await _uow.SaveChangesAsync(); }
        catch (DbUpdateException)
        {
            return Result.Failure("Không thể gia nhập: bạn đã thuộc một đội khác trong tựa game này.");
        }
        return Result.Success($"Đã gia nhập đội {m.Team.Name}.");
    }

    // Chỉ tài khoản tuyển thủ thuộc đội: Ban tổ chức / Admin không có đội (Admin cấp tài khoản BTC riêng, UC-43).
    private const string NotPlayerMessage = "Chỉ tài khoản tuyển thủ được tham gia đội. Ban tổ chức và quản trị viên dùng tài khoản riêng, không có đội.";

    private Task<bool> IsPlayerAsync(int accountId)
        => _uow.Repository<Account>().AnyAsync(a => a.AccountId == accountId && a.Role == "PLAYER");

    private async Task NotifyCaptainAsync(Team team, string body)
        => await _uow.Repository<Notification>().AddAsync(new Notification
        {
            AccountId = team.CaptainAccountId,
            NotificationType = "SYSTEM",
            Title = "Phản hồi lời mời vào đội",
            Body = body,
            LinkUrl = "/team/roster.html?id=" + team.TeamId,
            Channel = "IN_APP",
            IsRead = false,
            SentAt = DateTime.UtcNow
        });

    public async Task<IReadOnlyList<TeamListItemDto>> GetAllTeamsAsync(TeamSearchRequest? filter = null)
        => await ToListItems(FilterTeams(filter ?? new TeamSearchRequest())).ToListAsync();

    private static readonly HashSet<string> TeamStatuses = new() { "ACTIVE", "DISBANDED", "ALL" };
    private static readonly HashSet<string> TeamSorts = new() { "newest", "name", "members" };
    private const int MaxTeamPageSize = 48;

    public async Task<Result<TeamListPage>> SearchTeamsAsync(TeamSearchRequest filter)
    {
        // Kiểm tra từng tham số trước khi truy vấn — URL do người dùng sửa tay vẫn nhận lỗi tiếng Việt rõ ràng.
        var q = (filter.Q ?? string.Empty).Trim();
        if (q.Length > 100) return Result<TeamListPage>.FieldFailure("Q", "Từ khóa tìm kiếm tối đa 100 ký tự.");
        if (filter.GameId is <= 0) return Result<TeamListPage>.FieldFailure("GameId", "Tựa game không hợp lệ.");
        var status = string.IsNullOrWhiteSpace(filter.Status) ? "ACTIVE" : filter.Status.Trim().ToUpperInvariant();
        if (!TeamStatuses.Contains(status)) return Result<TeamListPage>.FieldFailure("Status", "Trạng thái lọc không hợp lệ.");
        var sort = string.IsNullOrWhiteSpace(filter.Sort) ? "newest" : filter.Sort.Trim().ToLowerInvariant();
        if (!TeamSorts.Contains(sort)) return Result<TeamListPage>.FieldFailure("Sort", "Cách sắp xếp không hợp lệ.");
        if (filter.Page < 1) return Result<TeamListPage>.FieldFailure("Page", "Số trang phải từ 1 trở lên.");
        if (filter.PageSize < 1 || filter.PageSize > MaxTeamPageSize)
            return Result<TeamListPage>.FieldFailure("PageSize", $"Số đội mỗi trang phải từ 1 đến {MaxTeamPageSize}.");

        var query = FilterTeams(new TeamSearchRequest { Q = q, GameId = filter.GameId, Status = status, Sort = sort });
        var total = await query.CountAsync();
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)filter.PageSize));
        // Trang vượt quá (ví dụ sau khi lọc hẹp lại) → về trang cuối thay vì trả trang rỗng.
        var page = Math.Min(filter.Page, totalPages);
        var items = await ToListItems(query.Skip((page - 1) * filter.PageSize).Take(filter.PageSize)).ToListAsync();
        return Result<TeamListPage>.Success(new TeamListPage(items, total, page, filter.PageSize, totalPages));
    }

    private static IQueryable<TeamListItemDto> ToListItems(IQueryable<Team> query) => query
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
        });

    private IQueryable<Team> FilterTeams(TeamSearchRequest filter)
    {
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

        // TeamId là khóa phụ để thứ tự ổn định giữa các trang (hai đội trùng tên/ngày tạo không nhảy trang).
        return filter.Sort switch
        {
            "name" => query.OrderBy(t => t.Name).ThenBy(t => t.TeamId),
            "members" => query.OrderByDescending(t => t.TeamMembers.Count(m => m.Status == "ACTIVE")).ThenBy(t => t.TeamId),
            _ => query.OrderByDescending(t => t.CreatedAt).ThenByDescending(t => t.TeamId)
        };
    }

    // Trận chưa kết thúc của đội (chờ điểm danh → đang tranh chấp), sớm nhất lên trước; trận chưa xếp giờ để cuối.
    public async Task<IReadOnlyList<TeamMatchItemDto>> GetUpcomingMatchesAsync(int teamId)
    {
        var open = new[] { "PENDING", "READY", "LIVE", "WAITING_REPORT", "DISPUTED" };
        var lobby = new[] { "READY", "LIVE", "WAITING_REPORT", "DISPUTED" };
        var rows = await _uow.Repository<Match>().Query().AsNoTracking()
            .Where(m => (m.TeamAid == teamId || m.TeamBid == teamId) && m.TeamAid != null && m.TeamBid != null
                        && open.Contains(m.Status)
                        && m.Tournament.Status != "DRAFT" && m.Tournament.Status != "CANCELLED")
            .OrderBy(m => m.ScheduledAt == null).ThenBy(m => m.ScheduledAt).ThenBy(m => m.MatchId)
            .Take(10)
            .Select(m => new
            {
                m.MatchId, m.TournamentId, TournamentName = m.Tournament.Name, m.ScheduledAt, m.Status,
                Opponent = m.TeamAid == teamId ? m.TeamB!.Name : m.TeamA!.Name
            })
            .ToListAsync();
        return rows.Select(r => new TeamMatchItemDto(r.MatchId, r.TournamentId, r.TournamentName, r.Opponent,
            r.ScheduledAt, r.Status, StatusText.Match(r.Status), lobby.Contains(r.Status))).ToList();
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
                Description = t.Description,
                GameTitle = t.Game.Title,
                GameMinStarters = t.Game.MinStarters,
                GameMaxStarters = t.Game.MaxStarters,
                GameMaxSubstitutes = t.Game.MaxSubstitutes,
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
        if (!await IsPlayerAsync(accountId))
            return Result<int>.Failure(NotPlayerMessage, StatusCodes.Status403Forbidden);

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
        if (await IsRosterLockedAsync(teamId)) return Result.Failure("Đội hình đang bị khóa vì đội có giải đã chốt nhánh hoặc đang thi đấu.");
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

    // Đổi chỗ một tuyển thủ chính thức với một tuyển thủ dự bị trong cùng một lần — dùng khi đã đủ cả hai loại suất
    // (trước đây phải loại bớt một người mới đổi được). Đội trưởng không đổi vị trí.
    public async Task<Result> SwapSlotsAsync(int teamId, int starterAccountId, int substituteAccountId, int captainAccountId)
    {
        var team = await _uow.Repository<Team>().GetByIdAsync(teamId);
        if (team is null) return Result.Failure("Không tìm thấy đội.");
        if (team.CaptainAccountId != captainAccountId) return Result.Failure("Chỉ đội trưởng được thao tác.");
        if (await IsRosterLockedAsync(teamId)) return Result.Failure("Đội hình đang bị khóa vì đội có giải đã chốt nhánh hoặc đang thi đấu.");
        if (starterAccountId == team.CaptainAccountId || substituteAccountId == team.CaptainAccountId)
            return Result.Failure("Không thể đổi vị trí của đội trưởng.");

        var members = await _uow.Repository<TeamMember>().Query()
            .Where(x => x.TeamId == teamId && x.Status == "ACTIVE" && (x.AccountId == starterAccountId || x.AccountId == substituteAccountId))
            .ToListAsync();
        var starter = members.FirstOrDefault(x => x.AccountId == starterAccountId);
        var sub = members.FirstOrDefault(x => x.AccountId == substituteAccountId);
        if (starter is null || sub is null) return Result.Failure("Thành viên không thuộc đội.");
        if (starter.Slot != "STARTER" || sub.Slot != "SUBSTITUTE")
            return Result.Failure("Cần chọn một tuyển thủ chính thức và một tuyển thủ dự bị để đổi chỗ.");

        starter.Slot = "SUBSTITUTE";
        sub.Slot = "STARTER";
        _uow.Repository<TeamMember>().Update(starter);
        _uow.Repository<TeamMember>().Update(sub);
        await _uow.SaveChangesAsync();
        return Result.Success("Đã đổi chỗ chính thức và dự bị.");
    }

    public async Task<Result> KickMemberAsync(int teamId, int targetAccountId, int captainAccountId)
    {
        var team = await _uow.Repository<Team>().GetByIdAsync(teamId);
        if (team is null) return Result.Failure("Không tìm thấy đội.");
        if (team.CaptainAccountId != captainAccountId) return Result.Failure("Chỉ đội trưởng được thao tác.");
        if (await IsRosterLockedAsync(teamId)) return Result.Failure("Đội hình đang bị khóa vì đội có giải đã chốt nhánh hoặc đang thi đấu.");
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
        if (await IsRosterLockedAsync(teamId)) return Result.Failure("Đội hình đang bị khóa vì đội có giải đã chốt nhánh hoặc đang thi đấu.");

        var m = await _uow.Repository<TeamMember>().Query()
            .FirstOrDefaultAsync(x => x.TeamId == teamId && x.AccountId == targetAccountId && x.Status == "PENDING");
        if (m is null) return Result.Failure("Không tìm thấy đơn chờ duyệt.");
        // Lời mời do đội gửi phải do chính tuyển thủ chấp nhận — đội trưởng không duyệt thay.
        if (m.Direction == "INVITED") return Result.Failure("Đây là lời mời đội đã gửi, đang chờ tuyển thủ chấp nhận. Đội trưởng chỉ có thể hủy lời mời.");
        if (!await IsPlayerAsync(targetAccountId)) return Result.Failure("Người này không phải tài khoản tuyển thủ (Ban tổ chức / quản trị viên không tham gia đội).");

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
        return Result.Success(m.Direction == "INVITED" ? "Đã hủy lời mời." : "Đã từ chối đơn.");
    }

    // ===== TEAM-07: Chuyển quyền đội trưởng =====
    public async Task<Result> TransferCaptainAsync(int teamId, int newCaptainAccountId, int currentCaptainAccountId)
    {
        var team = await _uow.Repository<Team>().GetByIdAsync(teamId);
        if (team is null) return Result.Failure("Không tìm thấy đội.");
        if (team.CaptainAccountId != currentCaptainAccountId) return Result.Failure("Chỉ đội trưởng hiện tại được chuyển quyền.");
        if (newCaptainAccountId == currentCaptainAccountId) return Result.Failure("Người nhận phải khác đội trưởng hiện tại.");
        if (await IsRosterLockedAsync(teamId)) return Result.Failure("Đội hình đang bị khóa vì đội có giải đã chốt nhánh hoặc đang thi đấu.");

        var newCap = await _uow.Repository<TeamMember>().Query()
            .FirstOrDefaultAsync(m => m.TeamId == teamId && m.AccountId == newCaptainAccountId && m.Status == "ACTIVE");
        if (newCap is null) return Result.Failure("Người nhận phải là thành viên đang hoạt động của đội.");

        // Đội trưởng mới nên là chính thức — nếu đang là dự bị thì đưa lên chính thức, nhưng
        // phải còn suất chính thức theo luật (giống kiểm tra ở ChangeSlotAsync).
        if (newCap.Slot != "STARTER")
        {
            var game = await _uow.Repository<Game>().GetByIdAsync(team.GameId);
            var (starters, _) = await CountSlotsAsync(teamId);
            if (game is not null && starters >= game.MaxStarters)
                return Result.Failure($"Đã đủ {game.MaxStarters} suất chính thức theo luật — không thể đưa người nhận lên chính thức.");
            newCap.Slot = "STARTER";
            _uow.Repository<TeamMember>().Update(newCap);
        }
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
            return Result.Failure("Đội hình đang bị khóa vì đội có giải đã chốt nhánh hoặc đang thi đấu. Vui lòng liên hệ BTC/Trọng tài.");

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

    // Danh mục game gần như không đổi — nhớ 60 giây để ô lọc không tốn một lượt database ở xa mỗi lần mở trang.
    public async Task<IReadOnlyList<GameOptionDto>> GetActiveGamesAsync()
        => (await _cache.GetOrCreateAsync("lookup:active-games", async e =>
        {
            e.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60);
            return (IReadOnlyList<GameOptionDto>)await _uow.Repository<Game>().Query()
                .Where(g => g.Status == "ACTIVE").OrderBy(g => g.Title)
                .Select(g => new GameOptionDto { GameId = g.GameId, Title = g.Title, MaxStarters = g.MaxStarters, MaxSubstitutes = g.MaxSubstitutes })
                .ToListAsync();
        }))!;

    public async Task<MyTeamDto?> GetMyTeamAsync(int accountId)
        => await _uow.Repository<TeamMember>().Query()
            .Where(m => m.AccountId == accountId && m.Status == "ACTIVE")
            .OrderBy(m => m.TeamId)
            .Select(m => new MyTeamDto { TeamId = m.Team.TeamId, Name = m.Team.Name, Tag = m.Team.Tag })
            .FirstOrDefaultAsync();

    public async Task<IReadOnlyList<MyTeamItemDto>> GetMyTeamsAsync(int accountId)
    {
        var teams = await _uow.Repository<TeamMember>().Query()
            .Where(m => m.AccountId == accountId && m.Status == "ACTIVE" && m.Team.Status == "ACTIVE")
            .Select(m => new MyTeamItemDto
            {
                TeamId = m.Team.TeamId,
                Name = m.Team.Name,
                Tag = m.Team.Tag,
                LogoUrl = m.Team.LogoUrl,
                GameId = m.Team.GameId,
                GameTitle = m.Team.Game.Title,
                IsCaptain = m.Team.CaptainAccountId == accountId,
                Slot = m.Slot,
                MemberCount = m.Team.TeamMembers.Count(x => x.Status == "ACTIVE"),
                JoinedAt = m.JoinedAt
            })
            .ToListAsync();
        return teams
            .OrderByDescending(t => t.IsCaptain)
            .ThenBy(t => t.GameTitle)
            .ThenBy(t => t.Name)
            .ToList();
    }

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

    // Roster khóa khi đội có đơn đã duyệt ở giải đang diễn ra (TEAM-03) HOẶC giải đã khóa nhánh mà chưa kết thúc
    // (BRK-03: "Chốt sơ đồ, khóa Roster của mọi đội") — để đội không tụt dưới số người thi đấu sau khi đã chốt.
    private Task<bool> IsRosterLockedAsync(int teamId)
        => _uow.Repository<Registration>().AnyAsync(r =>
            r.TeamId == teamId && r.Status == "APPROVED"
            && (r.Tournament.Status == "ONGOING"
                || (r.Tournament.BracketLockedAt != null && r.Tournament.Status != "COMPLETED" && r.Tournament.Status != "CANCELLED")));

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
