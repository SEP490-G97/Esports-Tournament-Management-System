using ETOMS_API.DTOs.Recruitment;
using ETOMS_API.Models;
using ETOMS_API.Repositories; // Thư mục chứa EtomsDbContext
using ETOMS_API.Services.Interface;
using Microsoft.EntityFrameworkCore;

namespace ETOMS_API.Services.Impl
{
    public class RecruitmentService : IRecruitmentService
    {
        // Giá trị PostType theo CHECK của lược đồ (CK_RecruitmentPost_Type). API/giao diện vẫn dùng
        // mã ngắn LFP/LFT; trước đây ghi thẳng "LFP"/"LFT" nên DB từ chối mọi tin đăng.
        private const string PostLookingForPlayer = "LOOKING_FOR_PLAYER";
        private const string PostLookingForTeam = "LOOKING_FOR_TEAM";

        private readonly EtomsDbContext _context;

        public RecruitmentService(EtomsDbContext context)
        {
            _context = context;
        }

        // FA-01: Đăng tin tuyển thành viên (LFP)
        // Tìm đội / tuyển người chỉ dành cho tuyển thủ: Ban tổ chức và Admin không có đội (quy tắc 15/11/2026).
        private async Task EnsurePlayerAsync(int accountId)
        {
            var role = await _context.Accounts.Where(a => a.AccountId == accountId).Select(a => a.Role).FirstOrDefaultAsync();
            if (role != "PLAYER")
                throw new InvalidOperationException("Chỉ tài khoản tuyển thủ được đăng tin tìm đội, tuyển người hoặc ứng tuyển. Ban tổ chức và quản trị viên không tham gia đội.");
        }

        public async Task<int> CreateLFPPostAsync(int accountId, CreateLFPPostDto dto)
        {
            await EnsurePlayerAsync(accountId);
            var team = await _context.Teams
                .Include(t => t.Game)
                .Include(t => t.TeamMembers)
                .FirstOrDefaultAsync(t => t.TeamId == dto.TeamId);

            if (team == null)
                throw new KeyNotFoundException("Không tìm thấy thông tin đội tuyển.");

            if (team.CaptainAccountId != accountId)
                throw new UnauthorizedAccessException("Chỉ có Đội trưởng mới được đăng tin tuyển người.");

            int activeMembersCount = team.TeamMembers.Count(tm => tm.Status == "ACTIVE");
            int maxRoster = team.Game.MaxStarters + team.Game.MaxSubstitutes;

            if (activeMembersCount >= maxRoster)
                throw new InvalidOperationException("Đội đã đủ người (chính thức & dự bị), không thể đăng tin tuyển thêm.");

            var post = new RecruitmentPost
            {
                PostType = PostLookingForPlayer,
                TeamId = dto.TeamId,
                AuthorAccountId = accountId,
                GameId = team.GameId,
                Title = $"[LFP] Tuyển vị trí {dto.RequiredRole} ({dto.RequiredRank})",
                Content = $"Vị trí cần tuyển: {dto.RequiredRole}\nRank yêu cầu: {dto.RequiredRank}\nMô tả: {dto.Description}",
                Status = "OPEN",
                CreatedAt = DateTime.UtcNow
            };

            _context.RecruitmentPosts.Add(post);
            await _context.SaveChangesAsync();
            return post.RecruitmentPostId;
        }

        // FA-02: Đăng tin tìm đội (LFT)
        public async Task<int> CreateLFTPostAsync(int accountId, CreateLFTPostDto dto)
        {
            await EnsurePlayerAsync(accountId);
            var hasCredential = await _context.PlayerCredentials
                .AnyAsync(pc => pc.AccountId == accountId && pc.GameId == dto.GameId);
            if (!hasCredential)
                throw new InvalidOperationException("Bạn chưa cập nhật thông tin In-game (IGN) cho Game này.");

            var oldPosts = await _context.RecruitmentPosts
                .Where(p => p.AuthorAccountId == accountId && p.PostType == PostLookingForTeam && p.GameId == dto.GameId && p.Status == "OPEN")
                .ToListAsync();

            foreach (var oldPost in oldPosts)
            {
                oldPost.Status = "CLOSED";   // CK_RecruitmentPost_Status chỉ cho OPEN/CLOSED; tin mới thay tin cũ
            }

            var post = new RecruitmentPost
            {
                PostType = PostLookingForTeam,
                AuthorAccountId = accountId,
                GameId = dto.GameId,
                Title = $"[LFT] Tìm đội thi đấu - Vị trí {dto.PreferredRole} ({dto.CurrentRank})",
                Content = $"Vị trí sở trường: {dto.PreferredRole}\nRank hiện tại: {dto.CurrentRank}\nMô tả: {dto.Description}",
                Status = "OPEN",
                CreatedAt = DateTime.UtcNow
            };

            _context.RecruitmentPosts.Add(post);
            await _context.SaveChangesAsync();
            return post.RecruitmentPostId;
        }

        // FA-03: Bảng tin Tuyển quân - Join trực tiếp bảng Game & Team lấy Tên thật
        public async Task<object> GetRecruitmentBoardAsync(RecruitmentBoardQueryDto query)
        {
            // Tham số trên URL do client gửi: chặn trang <= 0 (Skip âm -> lỗi 500) và trang quá lớn.
            var postType = query.PostType == "LFT" ? PostLookingForTeam : PostLookingForPlayer;
            query.PageIndex = Math.Max(1, query.PageIndex);
            query.PageSize = Math.Clamp(query.PageSize, 1, 50);

            var postsQuery = _context.RecruitmentPosts
                .Include(p => p.AuthorAccount)
                .Include(p => p.Team)
                .Include(p => p.Game) // Join thêm bảng Game
                .Where(p => p.Status == "OPEN" && p.PostType == postType)
                .AsQueryable();

            if (query.GameId.HasValue)
                postsQuery = postsQuery.Where(p => p.GameId == query.GameId || (p.Team != null && p.Team.GameId == query.GameId));

            if (!string.IsNullOrEmpty(query.Role))
                postsQuery = postsQuery.Where(p => p.Title.Contains(query.Role) || (p.Content != null && p.Content.Contains(query.Role)));

            if (!string.IsNullOrEmpty(query.Rank))
                postsQuery = postsQuery.Where(p => p.Title.Contains(query.Rank) || (p.Content != null && p.Content.Contains(query.Rank)));

            int totalItems = await postsQuery.CountAsync();
            var items = await postsQuery
                .OrderByDescending(p => p.CreatedAt)
                .Skip((query.PageIndex - 1) * query.PageSize)
                .Take(query.PageSize)
                .Select(p => new
                {
                    PostId = p.RecruitmentPostId,
                    // TeamId (tin LFP) để biết mình đã ứng tuyển chưa; GameId để đội trưởng mời đúng đội cùng tựa game (tin LFT).
                    p.TeamId,
                    p.GameId,
                    PostType = p.PostType == PostLookingForTeam ? "LFT" : "LFP",
                    p.Title,
                    p.Content,
                    p.HighlightUrl,
                    p.AuthorAccountId,
                    AuthorName = p.AuthorAccount.FullName,
                    AuthorAvatar = p.AuthorAccount.AvatarUrl,
                    TeamName = p.Team != null ? p.Team.Name : null,
                    TeamTag = p.Team != null ? p.Team.Tag : null,
                    TeamLogo = p.Team != null ? p.Team.LogoUrl : null,
                    // Lấy Tên Game trực tiếp từ DB
                    GameTitle = p.Game != null ? p.Game.Title : (p.Team != null && p.Team.Game != null ? p.Team.Game.Title : "Esports chung"),
                    p.CreatedAt
                })
                .ToListAsync();

            return new { TotalItems = totalItems, PageIndex = query.PageIndex, PageSize = query.PageSize, Data = items };
        }

        // FA-04: Nộp đơn ứng tuyển theo tin LFP
        public async Task<bool> ApplyToLFPAsync(int accountId, ApplyLFPPostDto dto)
        {
            await EnsurePlayerAsync(accountId);
            var post = await _context.RecruitmentPosts
                .Include(p => p.Team)
                .FirstOrDefaultAsync(p => p.RecruitmentPostId == dto.PostId && p.Status == "OPEN");

            if (post == null || !post.TeamId.HasValue || post.Team is null)
                throw new KeyNotFoundException("Bài đăng tuyển không tồn tại hoặc đã đóng.");

            var alreadyInThisTeam = await _context.TeamMembers
                .AnyAsync(tm => tm.AccountId == accountId && tm.Status == "ACTIVE" && tm.TeamId == post.TeamId);
            if (alreadyInThisTeam)
                throw new InvalidOperationException("Bạn đã là thành viên của đội này.");

            var inAnotherTeam = await _context.TeamMembers
                .AnyAsync(tm => tm.AccountId == accountId && tm.Status == "ACTIVE" && tm.Team.GameId == post.Team.GameId);
            if (inAnotherTeam)
                throw new InvalidOperationException("Bạn đã tham gia một đội tuyển khác của tựa game này (mỗi tựa game chỉ được ở một đội).");

            var existingMember = await _context.TeamMembers
                .AnyAsync(tm => tm.TeamId == post.TeamId && tm.AccountId == accountId && tm.Status == "PENDING");
            if (existingMember)
                throw new InvalidOperationException("Bạn đã nộp đơn ứng tuyển vào đội này và đang chờ duyệt.");

            var pendingMember = new TeamMember
            {
                TeamId = post.TeamId.Value,
                GameId = post.Team.GameId,   // cột bắt buộc (FK + chỉ mục "mỗi tựa game một đội")
                AccountId = accountId,
                Slot = "STARTER",
                Status = "PENDING",
                Direction = "APPLIED",
                JoinedAt = DateTime.UtcNow
            };

            _context.TeamMembers.Add(pendingMember);
            await _context.SaveChangesAsync();
            return true;
        }

        // FA-05: Duyệt đơn ứng tuyển
        public async Task<bool> ReviewApplicationAsync(int captainAccountId, int teamMemberId, ReviewApplicationDto dto)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var member = await _context.TeamMembers
                    .Include(m => m.Team).ThenInclude(t => t.Game)
                    .Include(m => m.Team).ThenInclude(t => t.TeamMembers)
                    .FirstOrDefaultAsync(m => m.TeamMemberId == teamMemberId && m.Status == "PENDING");

                if (member == null)
                    throw new KeyNotFoundException("Yêu cầu ứng tuyển không tồn tại hoặc đã xử lý.");

                if (member.Team.CaptainAccountId != captainAccountId)
                    throw new UnauthorizedAccessException("Bạn không phải Đội trưởng của đội tuyển này.");

                if (!dto.IsAccepted)
                {
                    _context.TeamMembers.Remove(member);
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                    return true;
                }

                int currentStarters = member.Team.TeamMembers.Count(m => m.Status == "ACTIVE" && m.Slot == "STARTER");
                int currentSubs = member.Team.TeamMembers.Count(m => m.Status == "ACTIVE" && m.Slot == "SUBSTITUTE");

                string assignedSlot = "STARTER";
                if (currentStarters >= member.Team.Game.MaxStarters)
                {
                    if (currentSubs >= member.Team.Game.MaxSubstitutes)
                    {
                        throw new InvalidOperationException("Đội tuyển đã đủ cả suất chính thức lẫn dự bị.");
                    }
                    assignedSlot = "SUBSTITUTE";
                }

                member.Status = "ACTIVE";
                member.Slot = assignedSlot;

                int totalActiveAfterAdd = currentStarters + currentSubs + 1;
                int maxTotalRoster = member.Team.Game.MaxStarters + member.Team.Game.MaxSubstitutes;

                if (totalActiveAfterAdd >= maxTotalRoster)
                {
                    var otherPendingMembers = await _context.TeamMembers
                        .Where(m => m.TeamId == member.TeamId && m.TeamMemberId != teamMemberId && m.Status == "PENDING")
                        .ToListAsync();

                    _context.TeamMembers.RemoveRange(otherPendingMembers);

                    var openLfpPosts = await _context.RecruitmentPosts
                        .Where(p => p.TeamId == member.TeamId && p.Status == "OPEN")
                        .ToListAsync();

                    foreach (var post in openLfpPosts)
                    {
                        post.Status = "CLOSED";
                    }
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return true;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        // FA-06: Mời trực tiếp Free Agent
        public async Task<bool> InviteFreeAgentAsync(int captainAccountId, InviteFreeAgentDto dto)
        {
            var team = await _context.Teams.FindAsync(dto.TeamId);
            if (team == null)
                throw new KeyNotFoundException("Không tìm thấy thông tin đội.");

            if (team.CaptainAccountId != captainAccountId)
                throw new UnauthorizedAccessException("Bạn không phải Đội trưởng của đội này.");

            var player = await _context.Accounts.FindAsync(dto.PlayerAccountId);
            if (player == null || player.Status != "ACTIVE" || player.Role != "PLAYER")
                throw new KeyNotFoundException("Không tìm thấy tuyển thủ được mời.");
            if (dto.PlayerAccountId == captainAccountId)
                throw new InvalidOperationException("Không thể tự mời chính mình.");
            if (await _context.TeamMembers.AnyAsync(tm => tm.AccountId == dto.PlayerAccountId && tm.Status == "ACTIVE" && tm.GameId == team.GameId))
                throw new InvalidOperationException("Tuyển thủ này đã ở một đội của tựa game này.");
            if (await _context.TeamMembers.AnyAsync(tm => tm.AccountId == dto.PlayerAccountId && tm.TeamId == dto.TeamId && tm.Status == "PENDING"))
                throw new InvalidOperationException("Đã có lời mời/đơn đang chờ giữa đội và tuyển thủ này.");

            var inviteMember = new TeamMember
            {
                TeamId = dto.TeamId,
                GameId = team.GameId,
                AccountId = dto.PlayerAccountId,
                Slot = "STARTER",
                Status = "PENDING",
                Direction = "INVITED",
                JoinedAt = DateTime.UtcNow
            };

            _context.TeamMembers.Add(inviteMember);
            // Báo tuyển thủ được mời: bấm vào là tới trang Lời mời để chấp nhận / từ chối.
            _context.Notifications.Add(new Notification
            {
                AccountId = dto.PlayerAccountId,
                NotificationType = "SYSTEM",
                Title = "Lời mời vào đội",
                Body = $"Đội {team.Name} mời bạn tham gia. Mở trang Lời mời để chấp nhận hoặc từ chối.",
                LinkUrl = "/team/invites.html",
                Channel = "IN_APP",
                IsRead = false,
                SentAt = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();
            return true;
        }

        // Đơn ứng tuyển đang chờ của tuyển thủ (hiện ở Hồ sơ; trang Tìm đội dùng để ẩn nút "Ứng tuyển" đã nộp).
        public async Task<List<MyApplicationDto>> GetMyApplicationsAsync(int accountId)
        {
            return await _context.TeamMembers.AsNoTracking()
                .Where(m => m.AccountId == accountId && m.Status == "PENDING" && m.Direction == "APPLIED")
                .OrderByDescending(m => m.JoinedAt)
                .Select(m => new MyApplicationDto
                {
                    TeamMemberId = m.TeamMemberId,
                    TeamId = m.TeamId,
                    TeamName = m.Team.Name,
                    TeamTag = m.Team.Tag,
                    GameTitle = m.Team.Game.Title,
                    SentAt = m.JoinedAt
                })
                .ToListAsync();
        }

        // Rút đơn ứng tuyển đang chờ (chỉ người nộp). Ghi REJECTED như khi đội từ chối, để giữ lịch sử.
        public async Task<bool> WithdrawApplicationAsync(int accountId, int teamMemberId)
        {
            var m = await _context.TeamMembers.FirstOrDefaultAsync(x => x.TeamMemberId == teamMemberId
                && x.AccountId == accountId && x.Status == "PENDING" && x.Direction == "APPLIED")
                ?? throw new KeyNotFoundException("Không tìm thấy đơn ứng tuyển đang chờ.");
            m.Status = "REJECTED";
            await _context.SaveChangesAsync();
            return true;
        }
    }
}