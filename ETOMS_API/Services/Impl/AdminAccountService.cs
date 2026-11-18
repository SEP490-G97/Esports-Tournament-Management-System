using ETOMS_API.DTOs.Admin;
using ETOMS_API.Models;
using ETOMS_API.Repositories;
using ETOMS_API.Services.Interface;
using ETOMS_API.Utils;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace ETOMS_API.Services.Impl
{
    public sealed class AdminAccountService : IAdminAccountService
    {
        public const int MinReasonLength = 5;
        public const int MaxReasonLength = 500;   // khớp Account.BanReason varchar(500)
        public const int MaxPageSize = 100;

        private readonly EtomsDbContext _db;
        private readonly IEmailSender _email;
        private readonly SupportOptions _support;
        private readonly ILogger<AdminAccountService> _log;
        private readonly IMemoryCache _cache;

        public AdminAccountService(
            EtomsDbContext db,
            IEmailSender email,
            IOptions<SupportOptions> support,
            ILogger<AdminAccountService> log,
            IMemoryCache cache)
        {
            _db = db;
            _email = email;
            _support = support.Value;
            _log = log;
            _cache = cache;
        }

        // ---------------------------------------------------------------- ADM-01

        public async Task<AdminListResult> ListAsync(AccountListQuery query, CancellationToken ct = default)
        {
            var role = (query.Role ?? "").Trim().ToUpperInvariant();
            if (role.Length > 0 && role != AccountRoles.Player && role != AccountRoles.Organizer && role != AccountRoles.Admin)
                return new AdminListResult(AdminError.InvalidFilter, null);

            string? status = null;
            switch ((query.Status ?? "").Trim().ToUpperInvariant())
            {
                case "":
                case "ALL": break;
                case "ACTIVE": status = AccountStatuses.Active; break;
                case "BANNED": status = AccountStatuses.Banned; break;
                case "UNVERIFIED":
                case "PENDING_VERIFY": status = AccountStatuses.PendingVerify; break;
                default: return new AdminListResult(AdminError.InvalidFilter, null);
            }

            var page = Math.Max(1, query.Page);
            var pageSize = Math.Clamp(query.PageSize, 1, MaxPageSize);

            // Bộ lọc nền: tìm kiếm + role (dùng chung cho đếm theo tab và danh sách).
            IQueryable<Account> baseQuery = _db.Accounts.AsNoTracking();
            if (role.Length > 0)
                baseQuery = baseQuery.Where(a => a.Role == role);

            var term = (query.Q ?? "").Trim();
            if (term.Length > 100) term = term[..100];
            if (term.Length > 0)
                baseQuery = baseQuery.Where(a => a.Email.Contains(term) || a.FullName.Contains(term));

            var grouped = await baseQuery
                .GroupBy(a => a.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync(ct);

            int CountOf(string s) => grouped.FirstOrDefault(g => g.Status == s)?.Count ?? 0;
            var counts = new AccountStatusCounts(
                grouped.Sum(g => g.Count),
                CountOf(AccountStatuses.Active),
                CountOf(AccountStatuses.Banned),
                CountOf(AccountStatuses.PendingVerify));

            var filtered = status == null ? baseQuery : baseQuery.Where(a => a.Status == status);
            var total = status == null ? counts.All : await filtered.CountAsync(ct);

            var items = await filtered
                .OrderByDescending(a => a.AccountId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(a => new AccountListItem(
                    a.AccountId,
                    a.Email,
                    a.FullName,
                    a.AvatarUrl,
                    a.Role,
                    a.Status,
                    a.BanReason,
                    a.PasswordHash != null,
                    a.GoogleSubjectId != null,
                    a.EmailVerifiedAt,
                    a.CreatedAt))
                .ToListAsync(ct);

            return new AdminListResult(AdminError.None, new AccountListResponse(items, total, page, pageSize, counts));
        }

        // ---------------------------------------------------------------- ADM-02

        // Giải "còn chạy" của người bị khóa: chưa kết thúc / chưa hủy.
        private static readonly string[] OpenTournamentStatuses = { "DRAFT", "PUBLISHED", "ONGOING" };

        private static string OrgRoleLabel(string role) => role switch
        {
            "TOURNAMENT_OPERATOR" => "Điều hành giải",
            "MARKETING" => "Truyền thông",
            "REFEREE" => "Trọng tài",
            "CHAIR" => "Chủ nhiệm",
            _ => role
        };

        // Người nhận bàn giao hợp lệ: thành viên ĐANG HOẠT ĐỘNG của đơn vị, là tài khoản Ban tổ chức chưa bị khóa (Hướng 1).
        private IQueryable<OrganizationMember> HandoverCandidates(int targetId) => _db.OrganizationMembers.AsNoTracking()
            .Where(m => m.AccountId != targetId && m.Status == "ACTIVE"
                && m.Account.Role == AccountRoles.Organizer && m.Account.Status == AccountStatuses.Active);

        public async Task<BanImpactResponse?> GetBanImpactAsync(int targetId, CancellationToken ct = default)
        {
            if (!await _db.Accounts.AnyAsync(a => a.AccountId == targetId, ct)) return null;

            var orgs = await _db.OrganizationMembers.AsNoTracking()
                .Where(m => m.AccountId == targetId && m.Role == "CHAIR" && m.Status == "ACTIVE")
                .Select(m => new { m.OrganizationId, m.Organization.Name })
                .ToListAsync(ct);
            var orgIds = orgs.Select(o => o.OrganizationId).ToList();

            var candidates = orgIds.Count == 0 ? new() : await HandoverCandidates(targetId)
                .Where(m => orgIds.Contains(m.OrganizationId))
                .OrderBy(m => m.Account.FullName)
                .Select(m => new { m.OrganizationId, m.AccountId, m.Account.FullName, m.Account.Email, m.Role })
                .ToListAsync(ct);

            var open = await _db.Tournaments.AsNoTracking()
                .Where(t => t.OrganizerAccountId == targetId && OpenTournamentStatuses.Contains(t.Status))
                .Select(t => t.OrganizationId)
                .ToListAsync(ct);

            var list = orgs.Select(o => new BanImpactOrganization(
                o.OrganizationId, o.Name,
                open.Count(id => id == o.OrganizationId),
                candidates.Where(c => c.OrganizationId == o.OrganizationId)
                    .Select(c => new BanImpactCandidate(c.AccountId, c.FullName, c.Email, OrgRoleLabel(c.Role)))
                    .ToList())).ToList();
            return new BanImpactResponse(list, open.Count);
        }

        public async Task<AdminResult> BanAsync(int actorId, int targetId, string? reason, IReadOnlyList<ChairHandover>? handovers = null,
            bool confirmWithoutHandover = false, CancellationToken ct = default)
        {
            var clean = (reason ?? "").Trim();
            if (clean.Length < MinReasonLength || clean.Length > MaxReasonLength)
                return new AdminResult(AdminError.InvalidReason);

            if (actorId == targetId)
                return new AdminResult(AdminError.CannotBanSelf);

            var target = await _db.Accounts.AsNoTracking()
                .Where(a => a.AccountId == targetId)
                .Select(a => new { a.Email, a.FullName, a.Role, a.Status })
                .FirstOrDefaultAsync(ct);

            if (target == null) return new AdminResult(AdminError.NotFound);
            if (target.Role == AccountRoles.Admin) return new AdminResult(AdminError.CannotBanAdmin);
            if (target.Status == AccountStatuses.Banned) return new AdminResult(AdminError.AlreadyBanned);

            // Đang là chủ nhiệm đơn vị: Admin phải chọn người nhận bàn giao cho từng đơn vị, hoặc xác nhận
            // khóa mà không bàn giao (đã được cảnh báo đơn vị sẽ tạm không có chủ nhiệm).
            var chairedOrgs = await _db.OrganizationMembers.AsNoTracking()
                .Where(m => m.AccountId == targetId && m.Role == "CHAIR" && m.Status == "ACTIVE")
                .Select(m => new { m.OrganizationId, m.Organization.Name })
                .ToListAsync(ct);
            var plan = (handovers ?? Array.Empty<ChairHandover>())
                .GroupBy(h => h.OrganizationId).Select(g => g.First()).ToList();
            if (plan.Any(h => chairedOrgs.All(o => o.OrganizationId != h.OrganizationId)))
                return new AdminResult(AdminError.InvalidHandover);
            if (chairedOrgs.Any(o => plan.All(h => h.OrganizationId != o.OrganizationId)) && !confirmWithoutHandover)
                return new AdminResult(AdminError.ChairHandoverRequired);
            foreach (var h in plan)
                if (!await HandoverCandidates(targetId).AnyAsync(m => m.OrganizationId == h.OrganizationId && m.AccountId == h.NewChairAccountId, ct))
                    return new AdminResult(AdminError.InvalidHandover);

            var now = DateTime.UtcNow;

            await using (var tx = await _db.Database.BeginTransactionAsync(ct))
            {
                // Điều kiện Status <> BANNED làm thao tác atomic: hai admin cùng khóa thì chỉ một người thắng.
                var changed = await _db.Accounts
                    .Where(a => a.AccountId == targetId && a.Status != AccountStatuses.Banned)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(a => a.Status, AccountStatuses.Banned)
                        .SetProperty(a => a.BanReason, clean), ct);

                if (changed == 0) return new AdminResult(AdminError.AlreadyBanned);

                // Đăng xuất mọi thiết bị: thu hồi toàn bộ refresh token còn hiệu lực.
                await _db.AuthTokens
                    .Where(t => t.AccountId == targetId && t.TokenType == TokenTypes.Refresh && t.UsedAt == null)
                    .ExecuteUpdateAsync(s => s.SetProperty(t => t.UsedAt, now), ct);

                _db.AuditLogs.Add(new AuditLog
                {
                    ActorAccountId = actorId,
                    TournamentId = null,
                    ActionType = "BAN",
                    ObjectType = "ACCOUNT",
                    ObjectId = targetId,
                    BeforeValue = Json(new { status = target.Status }),
                    AfterValue = Json(new { status = AccountStatuses.Banned }),
                    Reason = clean,
                    CreatedAt = now
                });

                // Bàn giao chủ nhiệm theo đúng quy ước lược đồ: CHAIR cũ -> TOURNAMENT_OPERATOR rồi người mới -> CHAIR
                // (2 lệnh riêng theo thứ tự, vì chỉ mục UX_OrganizationMember_Chair chỉ cho 1 chủ nhiệm/đơn vị),
                // ghi AuditLog TRANSFER_CHAIR. Giải chưa kết thúc của đơn vị do người bị khóa quản lý chuyển sang
                // chủ nhiệm mới để giải không bị bỏ trống người quản lý.
                foreach (var h in plan)
                {
                    await _db.OrganizationMembers
                        .Where(m => m.OrganizationId == h.OrganizationId && m.AccountId == targetId && m.Role == "CHAIR" && m.Status == "ACTIVE")
                        .ExecuteUpdateAsync(s => s.SetProperty(m => m.Role, "TOURNAMENT_OPERATOR"), ct);
                    await _db.OrganizationMembers
                        .Where(m => m.OrganizationId == h.OrganizationId && m.AccountId == h.NewChairAccountId && m.Status == "ACTIVE")
                        .ExecuteUpdateAsync(s => s.SetProperty(m => m.Role, "CHAIR"), ct);
                    var moved = await _db.Tournaments
                        .Where(t => t.OrganizationId == h.OrganizationId && t.OrganizerAccountId == targetId && OpenTournamentStatuses.Contains(t.Status))
                        .ExecuteUpdateAsync(s => s.SetProperty(t => t.OrganizerAccountId, h.NewChairAccountId), ct);

                    var orgName = chairedOrgs.First(o => o.OrganizationId == h.OrganizationId).Name;
                    _db.AuditLogs.Add(new AuditLog
                    {
                        ActorAccountId = actorId,
                        ActionType = "TRANSFER_CHAIR",
                        ObjectType = "ORGANIZATION",
                        ObjectId = h.OrganizationId,
                        BeforeValue = Json(new { chairAccountId = targetId }),
                        AfterValue = Json(new { chairAccountId = h.NewChairAccountId, tournamentsMoved = moved }),
                        Reason = "Bàn giao khi khóa tài khoản chủ nhiệm: " + clean,
                        CreatedAt = now
                    });
                    _db.Notifications.Add(new Notification
                    {
                        AccountId = h.NewChairAccountId,
                        NotificationType = "SYSTEM",
                        Title = "Bạn được bàn giao chủ nhiệm đơn vị",
                        Body = $"Quản trị viên đã bàn giao chủ nhiệm đơn vị \"{orgName}\" cho bạn vì tài khoản chủ nhiệm cũ bị khóa."
                            + (moved > 0 ? $" {moved} giải chưa kết thúc của đơn vị đã chuyển sang bạn quản lý." : ""),
                        LinkUrl = "/organization/details.html?id=" + h.OrganizationId,
                        Channel = "IN_APP",
                        IsRead = false,
                        SentAt = now
                    });
                }
                await _db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
            }

            // Bỏ trạng thái "đang hoạt động" đã nhớ để lần gọi API kế tiếp bị chặn ngay (AccountStatusCache).
            _cache.Remove(AccountStatusCache.Key(targetId));

            // Gửi mail sau khi commit: lỗi gửi mail không làm hỏng việc khóa (chỉ ghi log).
            await SafeSendAsync(target.Email,
                "[ETOMS] Tài khoản của bạn đã bị khóa",
                BuildBanEmail(target.FullName, clean), ct);

            return new AdminResult(AdminError.None);
        }

        // Cách 2 khi chủ nhiệm đơn vị vi phạm: CHƯA khóa, gửi thông báo trong hệ thống + email yêu cầu người đó tự
        // bàn giao chủ nhiệm (trang chi tiết đơn vị) trước; Admin khóa sau.
        public async Task<AdminResult> RequestChairHandoverAsync(int actorId, int targetId, string? reason, CancellationToken ct = default)
        {
            var clean = (reason ?? "").Trim();
            if (clean.Length < MinReasonLength || clean.Length > MaxReasonLength)
                return new AdminResult(AdminError.InvalidReason);

            var target = await _db.Accounts.AsNoTracking()
                .Where(a => a.AccountId == targetId)
                .Select(a => new { a.Email, a.FullName, a.Role, a.Status })
                .FirstOrDefaultAsync(ct);
            if (target == null) return new AdminResult(AdminError.NotFound);
            if (target.Status == AccountStatuses.Banned) return new AdminResult(AdminError.AlreadyBanned);

            var orgs = await _db.OrganizationMembers.AsNoTracking()
                .Where(m => m.AccountId == targetId && m.Role == "CHAIR" && m.Status == "ACTIVE")
                .Select(m => new { m.OrganizationId, m.Organization.Name })
                .ToListAsync(ct);
            if (orgs.Count == 0) return new AdminResult(AdminError.NotChair);

            var names = string.Join(", ", orgs.Select(o => "\"" + o.Name + "\""));
            var now = DateTime.UtcNow;
            _db.Notifications.Add(new Notification
            {
                AccountId = targetId,
                NotificationType = "SYSTEM",
                Title = "Yêu cầu bàn giao chủ nhiệm đơn vị",
                Body = $"Tài khoản của bạn sắp bị khóa. Lý do: {clean}. Vui lòng bàn giao chủ nhiệm đơn vị {names} cho một thành viên khác.",
                LinkUrl = "/organization/details.html?id=" + orgs[0].OrganizationId,
                Channel = "BOTH",   // email gửi riêng ngay bên dưới (không để bộ gửi tự động gửi trùng)
                IsRead = false,
                SentAt = now
            });
            await _db.SaveChangesAsync(ct);

            var name = WebUtility.HtmlEncode(target.FullName);
            var why = WebUtility.HtmlEncode(clean).Replace("\n", "<br/>");
            await SafeSendAsync(target.Email, "[ETOMS] Yêu cầu bàn giao chủ nhiệm đơn vị tổ chức",
                $@"<div style=""font-family:Arial,sans-serif;font-size:15px;line-height:1.6;color:#1f2430"">
                    <p>Xin chào <b>{name}</b>,</p>
                    <p>Tài khoản ETOMS của bạn đang được xem xét <b>khóa</b>. Lý do:<br/>{why}</p>
                    <p>Bạn đang là chủ nhiệm đơn vị {WebUtility.HtmlEncode(names)}. Vui lòng vào trang chi tiết đơn vị và
                    <b>bàn giao chủ nhiệm</b> cho một thành viên khác để đơn vị tiếp tục hoạt động.</p>
                    <p>{SupportBlock()}</p>
                    <p>Trân trọng,<br/>Đội ngũ ETOMS</p></div>", ct);
            return new AdminResult(AdminError.None);
        }

        public async Task<AdminResult> UnbanAsync(int actorId, int targetId, string? note, CancellationToken ct = default)
        {
            var cleanNote = (note ?? "").Trim();
            if (cleanNote.Length > MaxReasonLength)
                return new AdminResult(AdminError.InvalidReason);

            var target = await _db.Accounts.AsNoTracking()
                .Where(a => a.AccountId == targetId)
                .Select(a => new { a.Email, a.FullName, a.Status, a.BanReason, a.EmailVerifiedAt })
                .FirstOrDefaultAsync(ct);

            if (target == null) return new AdminResult(AdminError.NotFound);
            if (target.Status != AccountStatuses.Banned) return new AdminResult(AdminError.NotBanned);

            // Trả về đúng trạng thái trước khi khóa: đã xác minh email thì ACTIVE, chưa thì PENDING_VERIFY.
            var restored = target.EmailVerifiedAt != null ? AccountStatuses.Active : AccountStatuses.PendingVerify;
            var now = DateTime.UtcNow;

            await using (var tx = await _db.Database.BeginTransactionAsync(ct))
            {
                var changed = await _db.Accounts
                    .Where(a => a.AccountId == targetId && a.Status == AccountStatuses.Banned)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(a => a.Status, restored)
                        .SetProperty(a => a.BanReason, (string?)null), ct);

                if (changed == 0) return new AdminResult(AdminError.NotBanned);

                _db.AuditLogs.Add(new AuditLog
                {
                    ActorAccountId = actorId,
                    TournamentId = null,
                    ActionType = "UNBAN",
                    ObjectType = "ACCOUNT",
                    ObjectId = targetId,
                    BeforeValue = Json(new { status = AccountStatuses.Banned, banReason = target.BanReason }),
                    AfterValue = Json(new { status = restored }),
                    Reason = cleanNote.Length == 0 ? null : cleanNote,
                    CreatedAt = now
                });
                await _db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
            }

            await SafeSendAsync(target.Email,
                "[ETOMS] Tài khoản của bạn đã được mở khóa",
                BuildUnbanEmail(target.FullName), ct);

            return new AdminResult(AdminError.None);
        }

        // ---------------------------------------------------------------- helpers

        private static string Json(object o) => JsonSerializer.Serialize(o);

        private async Task SafeSendAsync(string to, string subject, string html, CancellationToken ct)
        {
            try
            {
                await _email.SendAsync(to, subject, html, ct);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Gửi mail '{Subject}' tới {To} thất bại", subject, to);
            }
        }

        private string SupportBlock()
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(_support.Email))
            {
                var e = WebUtility.HtmlEncode(_support.Email.Trim());
                parts.Add($"gửi email tới <a href=\"mailto:{e}\">{e}</a> (ghi rõ email tài khoản và nội dung khiếu nại)");
            }
            if (!string.IsNullOrWhiteSpace(_support.Url))
            {
                var u = WebUtility.HtmlEncode(_support.Url.Trim());
                parts.Add($"hoặc truy cập <a href=\"{u}\">{u}</a>");
            }
            return parts.Count == 0
                ? "Nếu bạn cho rằng đây là nhầm lẫn, vui lòng liên hệ ban quản trị ETOMS để được xem xét lại."
                : "Nếu bạn cho rằng đây là nhầm lẫn, bạn có thể khiếu nại bằng cách " + string.Join(" ", parts) + ".";
        }

        private string BuildBanEmail(string fullName, string reason)
        {
            var name = WebUtility.HtmlEncode(fullName);
            var why = WebUtility.HtmlEncode(reason).Replace("\n", "<br/>");
            return $@"<div style=""font-family:Arial,sans-serif;font-size:15px;line-height:1.6;color:#1f2430"">
                        <p>Xin chào <b>{name}</b>,</p>
                        <p>Tài khoản ETOMS của bạn đã bị <b>khóa</b> do vi phạm quy định của hệ thống.</p>
                        <p><b>Lý do:</b><br/>{why}</p>
                        <p>{SupportBlock()}</p>
                        <p>Trân trọng,<br/>Đội ngũ ETOMS</p></div>";
        }

        private static string BuildUnbanEmail(string fullName)
        {
            var name = WebUtility.HtmlEncode(fullName);
            return $@"<div style=""font-family:Arial,sans-serif;font-size:15px;line-height:1.6;color:#1f2430"">
                        <p>Xin chào <b>{name}</b>,</p>
                        <p>Tài khoản ETOMS của bạn đã được <b>mở khóa</b>. Bạn có thể đăng nhập lại bình thường.</p>
                        <p>Vui lòng tuân thủ quy định của hệ thống để tránh bị khóa lần nữa.</p>
                        <p>Trân trọng,<br/>Đội ngũ ETOMS</p></div>";
        }
    }
}
