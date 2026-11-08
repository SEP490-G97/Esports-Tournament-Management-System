// Services/Impl/RecruitmentService.cs
using ETOMS_API.DTOs.Recruitment;
using ETOMS_API.Models;
using ETOMS_API.Repositories;
using ETOMS_API.Services.Interface;
using Microsoft.EntityFrameworkCore;

namespace ETOMS_API.Services.Impl
{
    public class RecruitmentService : IRecruitmentService
    {
        private readonly EtomsDbContext _context;

        public RecruitmentService(EtomsDbContext context)
        {
            _context = context;
        }

        // FA-01: Đăng tin tuyển thành viên (LFP)
        public async Task<int> CreateLFPPostAsync(int accountId, CreateLFPPostDto dto)
        {
            var team = await _context.Teams
                .Include(t => t.Game)
                .Include(t => t.TeamMembers)
                .FirstOrDefaultAsync(t => t.TeamId == dto.TeamId);

            if (team == null)
                throw new KeyNotFoundException("Không tìm thấy thông tin đội tuyển.");

            // 1. Kiểm tra Đội trưởng
            if (team.CaptainAccountId != accountId)
                throw new UnauthorizedAccessException("Chỉ có Đội trưởng mới được đăng tin tuyển người.");

            // 2. Kiểm tra Roster đã đầy chưa
            int activeMembersCount = team.TeamMembers.Count(tm => tm.Status == "ACTIVE");
            int maxRoster = team.Game.MaxStarters + team.Game.MaxSubstitutes;

            if (activeMembersCount >= maxRoster)
                throw new InvalidOperationException("Đội đã đủ Roster (chính thức & dự bị), không thể đăng tin tuyển thêm.");

            // 3. Tạo bài đăng LFP
            var post = new RecruitmentPost
            {
                PostType = "LFP",
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
            // 1. Kiểm tra Credential game của Player
            var hasCredential = await _context.PlayerCredentials
                .AnyAsync(pc => pc.AccountId == accountId && pc.GameId == dto.GameId);
            if (!hasCredential)
                throw new InvalidOperationException("Bạn chưa cập nhật thông tin In-game (IGN) cho Game này.");

            // 2. Đóng bài đăng LFT cũ nếu có
            var oldPosts = await _context.RecruitmentPosts
                .Where(p => p.AuthorAccountId == accountId && p.PostType == "LFT" && p.GameId == dto.GameId && p.Status == "OPEN")
                .ToListAsync();

            foreach (var oldPost in oldPosts)
            {
                oldPost.Status = "EXPIRED";
            }

            // 3. Tạo bài LFT mới
            var post = new RecruitmentPost
            {
                PostType = "LFT",
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

        // FA-03: Bảng tin Tuyển quân (Team Finder Board)
        public async Task<object> GetRecruitmentBoardAsync(RecruitmentBoardQueryDto query)
        {
            var postsQuery = _context.RecruitmentPosts
                .Include(p => p.AuthorAccount)
                .Include(p => p.Team)
                .Where(p => p.Status == "OPEN" && p.PostType == query.PostType)
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
                    p.PostType,
                    p.Title,
                    p.Content,
                    p.HighlightUrl,
                    AuthorName = p.AuthorAccount.FullName,
                    AuthorAvatar = p.AuthorAccount.AvatarUrl,
                    TeamName = p.Team != null ? p.Team.Name : null,
                    TeamTag = p.Team != null ? p.Team.Tag : null,
                    TeamLogo = p.Team != null ? p.Team.LogoUrl : null,
                    p.CreatedAt
                })
                .ToListAsync();

            return new { TotalItems = totalItems, PageIndex = query.PageIndex, PageSize = query.PageSize, Data = items };
        }

        // FA-04: Nộp đơn ứng tuyển theo tin LFP
        public async Task<bool> ApplyToLFPAsync(int accountId, ApplyLFPPostDto dto)
        {
            var post = await _context.RecruitmentPosts
                .Include(p => p.Team)
                .FirstOrDefaultAsync(p => p.RecruitmentPostId == dto.PostId && p.Status == "OPEN");

            if (post == null || !post.TeamId.HasValue)
                throw new KeyNotFoundException("Bài đăng tuyển không tồn tại hoặc đã đóng.");

            // Check xem Player đã ở trong một đội tuyển ACTIVE khác của cùng Game này chưa
            var inAnotherTeam = await _context.TeamMembers
                .AnyAsync(tm => tm.AccountId == accountId && tm.Status == "ACTIVE" && tm.Team.GameId == post.Team.GameId);
            if (inAnotherTeam)
                throw new InvalidOperationException("Bạn đã tham gia một đội tuyển khác của Game này.");

            // Check xem đã nộp đơn xin gia nhập đội này chưa (qua bảng TeamMember ở trạng thái PENDING)
            var existingMember = await _context.TeamMembers
                .AnyAsync(tm => tm.TeamId == post.TeamId && tm.AccountId == accountId && tm.Status == "PENDING");
            if (existingMember)
                throw new InvalidOperationException("Bạn đã nộp đơn ứng tuyển vào đội này và đang chờ duyệt.");

            // Thêm bản ghi TeamMember trạng thái PENDING đại diện cho đơn xin nhập đội
            var pendingMember = new TeamMember
            {
                TeamId = post.TeamId.Value,
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

        // FA-05: Duyệt đơn ứng tuyển (Transaction-safe)
        public async Task<bool> ReviewApplicationAsync(int captainAccountId, int teamMemberId, ReviewApplicationDto dto)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var member = await _context.TeamMembers
                    .Include(m => m.Team)
                        .ThenInclude(t => t.Game)
                    .Include(m => m.Team)
                        .ThenInclude(t => t.TeamMembers)
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

                // Xếp loại Slot (STARTER hay SUBSTITUTE)
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

                // Nếu Roster max -> Hủy các đơn PENDING khác & Đóng các LFP Posts của đội
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

            var inviteMember = new TeamMember
            {
                TeamId = dto.TeamId,
                AccountId = dto.PlayerAccountId,
                Slot = "STARTER",
                Status = "PENDING",
                Direction = "INVITED",
                JoinedAt = DateTime.UtcNow
            };

            _context.TeamMembers.Add(inviteMember);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}