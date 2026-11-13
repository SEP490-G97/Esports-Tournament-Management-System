using ETOMS_API.DTOs.Admin;
using ETOMS_API.Models;
using ETOMS_API.Repositories;
using ETOMS_API.Services.Interface;
using ETOMS_API.Utils;
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

        public AdminAccountService(
            EtomsDbContext db,
            IEmailSender email,
            IOptions<SupportOptions> support,
            ILogger<AdminAccountService> log)
        {
            _db = db;
            _email = email;
            _support = support.Value;
            _log = log;
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

        public async Task<AdminResult> BanAsync(int actorId, int targetId, string? reason, CancellationToken ct = default)
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
                await _db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
            }

            // Gửi mail sau khi commit: lỗi gửi mail không làm hỏng việc khóa (chỉ ghi log).
            await SafeSendAsync(target.Email,
                "[ETOMS] Tài khoản của bạn đã bị khóa",
                BuildBanEmail(target.FullName, clean), ct);

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
