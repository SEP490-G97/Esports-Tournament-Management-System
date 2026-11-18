using System.Net;
using System.Security.Cryptography;
using ETOMS_API.DTOs.Admin;
using ETOMS_API.Models;
using ETOMS_API.Repositories;
using ETOMS_API.Services.Interface;
using ETOMS_API.Utils;
using Microsoft.EntityFrameworkCore;

namespace ETOMS_API.Services.Impl;

// UC-43 "grants the Organizer right": tuyển thủ gửi đơn kèm mong muốn; Admin duyệt bằng cách CẤP MỘT TÀI KHOẢN
// BAN TỔ CHỨC RIÊNG (email khác, role ORGANIZER, không có đội). Tài khoản tuyển thủ giữ nguyên đội của mình
// -> Ban tổ chức không bao giờ vừa có đội vừa tổ chức giải (quy tắc 15/11/2026).
public sealed class OrganizerRequestService : IOrganizerRequestService
{
    private const int NoteMin = 20, NoteMax = 1000, ReasonMin = 5, ReasonMax = 500;
    private readonly EtomsDbContext _db;
    private readonly IPasswordService _passwords;
    private readonly IEmailSender _email;
    private readonly ILogger<OrganizerRequestService> _log;

    public OrganizerRequestService(EtomsDbContext db, IPasswordService passwords, IEmailSender email, ILogger<OrganizerRequestService> log)
    {
        _db = db;
        _passwords = passwords;
        _email = email;
        _log = log;
    }

    private static string Label(string status) => status switch
    {
        "PENDING" => "Đang chờ quản trị viên xét duyệt",
        "APPROVED" => "Đã được cấp tài khoản Ban tổ chức",
        "REJECTED" => "Đã bị từ chối",
        _ => "Chưa gửi đơn"
    };

    private static MyOrganizerRequestDto ToMine(Account a) => new()
    {
        Role = a.Role,
        Status = a.OrganizerRequestStatus,
        StatusLabel = Label(a.OrganizerRequestStatus),
        RequestedAt = a.OrganizerRequestedAt,
        Note = a.OrganizerRequestNote,
        ProposedEmail = a.OrganizerRequestEmail,
        CanSubmit = a.Role == AccountRoles.Player && a.Status == "ACTIVE" && a.OrganizerRequestStatus is not ("PENDING" or "APPROVED")
    };

    public async Task<MyOrganizerRequestDto?> GetMineAsync(int accountId, CancellationToken ct = default)
    {
        var a = await _db.Accounts.AsNoTracking().FirstOrDefaultAsync(x => x.AccountId == accountId, ct);
        return a is null ? null : ToMine(a);
    }

    public async Task<Result<MyOrganizerRequestDto>> SubmitAsync(int accountId, SubmitOrganizerRequest request, CancellationToken ct = default)
    {
        var a = await _db.Accounts.FirstOrDefaultAsync(x => x.AccountId == accountId, ct);
        if (a is null) return Result.Failure<MyOrganizerRequestDto>("Không tìm thấy tài khoản.", StatusCodes.Status404NotFound);
        if (a.Role != AccountRoles.Player)
            return Result.Failure<MyOrganizerRequestDto>("Tài khoản này đã là Ban tổ chức hoặc quản trị viên.", StatusCodes.Status409Conflict);
        if (a.Status != "ACTIVE" || a.EmailVerifiedAt is null)
            return Result.Failure<MyOrganizerRequestDto>("Tài khoản phải đang hoạt động và đã xác minh email mới gửi được đơn.", StatusCodes.Status403Forbidden);
        if (a.OrganizerRequestStatus == "PENDING")
            return Result.Failure<MyOrganizerRequestDto>("Bạn đã có một đơn đang chờ xét duyệt.", StatusCodes.Status409Conflict);
        if (a.OrganizerRequestStatus == "APPROVED")
            return Result.Failure<MyOrganizerRequestDto>("Bạn đã được cấp tài khoản Ban tổ chức — hãy đăng nhập bằng tài khoản đó.", StatusCodes.Status409Conflict);

        var note = (request?.Note ?? string.Empty).Trim();
        if (note.Length < NoteMin || note.Length > NoteMax)
            return Result<MyOrganizerRequestDto>.FieldFailure("Note", $"Hãy ghi mong muốn / lý do muốn làm Ban tổ chức ({NoteMin}–{NoteMax} ký tự).");
        var proposed = string.IsNullOrWhiteSpace(request?.ProposedEmail) ? null : request!.ProposedEmail!.Trim().ToLowerInvariant();
        if (proposed is not null)
        {
            if (!PasswordPolicy.IsValidEmail(proposed) || proposed.Length > 254)
                return Result<MyOrganizerRequestDto>.FieldFailure("ProposedEmail", "Email đề xuất không hợp lệ.");
            if (string.Equals(proposed, a.Email, StringComparison.OrdinalIgnoreCase))
                return Result<MyOrganizerRequestDto>.FieldFailure("ProposedEmail", "Tài khoản Ban tổ chức là tài khoản riêng — hãy đề xuất một email khác email hiện tại.");
            if (await _db.Accounts.AnyAsync(x => x.Email == proposed, ct))
                return Result<MyOrganizerRequestDto>.FieldFailure("ProposedEmail", "Email này đã được dùng cho một tài khoản khác.");
        }

        a.OrganizerRequestStatus = "PENDING";
        a.OrganizerRequestedAt = DateTime.UtcNow;
        a.OrganizerRequestNote = note;
        a.OrganizerRequestEmail = proposed;
        foreach (var adminId in await _db.Accounts.Where(x => x.Role == AccountRoles.Admin && x.Status == "ACTIVE").Select(x => x.AccountId).ToListAsync(ct))
            _db.Notifications.Add(new Notification
            {
                AccountId = adminId, NotificationType = "SYSTEM", Channel = "IN_APP", IsRead = false, SentAt = DateTime.UtcNow,
                Title = "Đơn xin làm Ban tổ chức mới",
                Body = $"{a.FullName} xin làm Ban tổ chức. Xem mong muốn và cấp tài khoản ở trang Quản trị.",
                LinkUrl = "/admin/index.html#organizer-requests"
            });
        await _db.SaveChangesAsync(ct);
        return Result.Success(ToMine(a), "Đã gửi đơn. Quản trị viên sẽ xem xét và cấp tài khoản Ban tổ chức riêng nếu đồng ý.");
    }

    public async Task<Result<MyOrganizerRequestDto>> CancelAsync(int accountId, CancellationToken ct = default)
    {
        var a = await _db.Accounts.FirstOrDefaultAsync(x => x.AccountId == accountId, ct);
        if (a is null) return Result.Failure<MyOrganizerRequestDto>("Không tìm thấy tài khoản.", StatusCodes.Status404NotFound);
        if (a.OrganizerRequestStatus != "PENDING")
            return Result.Failure<MyOrganizerRequestDto>("Không có đơn nào đang chờ để hủy.", StatusCodes.Status409Conflict);
        a.OrganizerRequestStatus = "NONE";
        await _db.SaveChangesAsync(ct);
        return Result.Success(ToMine(a), "Đã hủy đơn xin làm Ban tổ chức.");
    }

    public async Task<IReadOnlyList<OrganizerRequestItemDto>> ListPendingAsync(CancellationToken ct = default)
        => await _db.Accounts.AsNoTracking()
            .Where(a => a.OrganizerRequestStatus == "PENDING")
            .OrderBy(a => a.OrganizerRequestedAt)
            .Select(a => new OrganizerRequestItemDto
            {
                AccountId = a.AccountId,
                FullName = a.FullName,
                Email = a.Email,
                RequestedAt = a.OrganizerRequestedAt ?? a.CreatedAt,
                Note = a.OrganizerRequestNote,
                ProposedEmail = a.OrganizerRequestEmail,
                ActiveTeams = a.TeamMembers.Count(m => m.Status == "ACTIVE")
            })
            .ToListAsync(ct);

    public async Task<Result<ApproveOrganizerResult>> ApproveAsync(int adminAccountId, int requesterAccountId, ApproveOrganizerRequest request, CancellationToken ct = default)
    {
        var requester = await _db.Accounts.FirstOrDefaultAsync(x => x.AccountId == requesterAccountId, ct);
        if (requester is null || requester.OrganizerRequestStatus != "PENDING")
            return Result.Failure<ApproveOrganizerResult>("Không tìm thấy đơn đang chờ xét duyệt.", StatusCodes.Status404NotFound);

        var email = (request?.OrganizerEmail ?? string.Empty).Trim().ToLowerInvariant();
        if (!PasswordPolicy.IsValidEmail(email) || email.Length > 254)
            return Result<ApproveOrganizerResult>.FieldFailure("OrganizerEmail", "Nhập email đăng nhập hợp lệ cho tài khoản Ban tổ chức.");
        if (string.Equals(email, requester.Email, StringComparison.OrdinalIgnoreCase))
            return Result<ApproveOrganizerResult>.FieldFailure("OrganizerEmail", "Tài khoản Ban tổ chức phải dùng email khác email tài khoản tuyển thủ.");
        if (await _db.Accounts.AnyAsync(x => x.Email == email, ct))
            return Result<ApproveOrganizerResult>.FieldFailure("OrganizerEmail", "Email này đã được dùng cho một tài khoản khác.");
        var name = string.IsNullOrWhiteSpace(request?.OrganizerFullName) ? $"{requester.FullName} (BTC)" : request!.OrganizerFullName!.Trim();
        if (name.Length < 2 || name.Length > 100)
            return Result<ApproveOrganizerResult>.FieldFailure("OrganizerFullName", "Tên hiển thị của Ban tổ chức phải từ 2 đến 100 ký tự.");

        var temp = TemporaryPassword();
        var now = DateTime.UtcNow;
        var organizer = new Account
        {
            Email = email,
            PasswordHash = _passwords.Hash(temp),
            FullName = name,
            Role = AccountRoles.Organizer,
            Status = "ACTIVE",
            EmailVerifiedAt = now,              // Admin bàn giao trực tiếp -> coi như đã xác minh
            NotifyEmail = true,
            NotifyInApp = true,
            OrganizerRequestStatus = "NONE",
            CreatedAt = now
        };
        _db.Accounts.Add(organizer);
        requester.OrganizerRequestStatus = "APPROVED";
        await _db.SaveChangesAsync(ct);

        _db.AuditLogs.Add(new AuditLog
        {
            ActorAccountId = adminAccountId, ActionType = "GRANT_ORGANIZER", ObjectType = "ACCOUNT", ObjectId = requester.AccountId,
            AfterValue = $"OrganizerAccountId={organizer.AccountId};Email={email}", Reason = "Cấp tài khoản Ban tổ chức riêng theo đơn (UC-43).", CreatedAt = now
        });
        _db.Notifications.Add(new Notification
        {
            AccountId = requester.AccountId, NotificationType = "SYSTEM", Channel = "BOTH", IsRead = false, SentAt = now,
            Title = "Đơn xin làm Ban tổ chức đã được duyệt",
            Body = $"Quản trị viên đã cấp tài khoản Ban tổ chức riêng: {email}. Đăng nhập bằng email này với mật khẩu tạm được gửi tới hộp thư đó, rồi đổi mật khẩu. Tài khoản tuyển thủ hiện tại của bạn giữ nguyên.",
            LinkUrl = "/auth/login.html"
        });
        await _db.SaveChangesAsync(ct);

        var sent = await TrySendAsync(email, "ETOMS — Tài khoản Ban tổ chức của bạn",
            $"<p>Chào {WebUtility.HtmlEncode(name)},</p><p>Quản trị viên ETOMS đã cấp cho bạn tài khoản <b>Ban tổ chức</b>.</p>" +
            $"<p>Email đăng nhập: <b>{WebUtility.HtmlEncode(email)}</b><br/>Mật khẩu tạm: <b>{WebUtility.HtmlEncode(temp)}</b></p>" +
            "<p>Hãy đăng nhập và đổi mật khẩu ngay. Tài khoản Ban tổ chức không tham gia đội thi đấu; tài khoản tuyển thủ của bạn vẫn giữ nguyên.</p>", ct);
        await TrySendAsync(requester.Email, "ETOMS — Đơn xin làm Ban tổ chức đã được duyệt",
            $"<p>Chào {WebUtility.HtmlEncode(requester.FullName)},</p><p>Đơn của bạn đã được duyệt. Tài khoản Ban tổ chức riêng: <b>{WebUtility.HtmlEncode(email)}</b> (mật khẩu tạm đã gửi tới hộp thư đó).</p>", ct);

        return Result.Success(new ApproveOrganizerResult { OrganizerAccountId = organizer.AccountId, OrganizerEmail = email, TemporaryPassword = temp, EmailSent = sent },
            "Đã cấp tài khoản Ban tổ chức " + email + ".");
    }

    public async Task<Result> RejectAsync(int adminAccountId, int requesterAccountId, RejectOrganizerRequest request, CancellationToken ct = default)
    {
        var requester = await _db.Accounts.FirstOrDefaultAsync(x => x.AccountId == requesterAccountId, ct);
        if (requester is null || requester.OrganizerRequestStatus != "PENDING")
            return Result.Failure("Không tìm thấy đơn đang chờ xét duyệt.", StatusCodes.Status404NotFound);
        var reason = (request?.Reason ?? string.Empty).Trim();
        if (reason.Length < ReasonMin || reason.Length > ReasonMax)
            return Result.FieldFailure("Reason", $"Từ chối phải ghi lý do ({ReasonMin}–{ReasonMax} ký tự).");

        var now = DateTime.UtcNow;
        requester.OrganizerRequestStatus = "REJECTED";
        _db.AuditLogs.Add(new AuditLog
        {
            ActorAccountId = adminAccountId, ActionType = "REJECT", ObjectType = "ACCOUNT", ObjectId = requester.AccountId,
            BeforeValue = "OrganizerRequestStatus=PENDING", AfterValue = "OrganizerRequestStatus=REJECTED", Reason = reason, CreatedAt = now
        });
        _db.Notifications.Add(new Notification
        {
            AccountId = requester.AccountId, NotificationType = "SYSTEM", Channel = "BOTH", IsRead = false, SentAt = now,
            Title = "Đơn xin làm Ban tổ chức bị từ chối",
            Body = "Lý do: " + reason + " — bạn có thể gửi lại đơn sau khi bổ sung.",
            LinkUrl = "/profile/index.html#organizer-request"
        });
        await _db.SaveChangesAsync(ct);
        await TrySendAsync(requester.Email, "ETOMS — Đơn xin làm Ban tổ chức bị từ chối",
            $"<p>Chào {WebUtility.HtmlEncode(requester.FullName)},</p><p>Đơn xin làm Ban tổ chức chưa được duyệt.</p><p>Lý do: {WebUtility.HtmlEncode(reason)}</p>", ct);
        return Result.Success("Đã từ chối đơn và gửi lý do cho người xin.");
    }

    // Mật khẩu tạm đạt PasswordPolicy: có chữ hoa, chữ thường, chữ số; 12 ký tự ngẫu nhiên an toàn.
    private static string TemporaryPassword()
    {
        const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ", lower = "abcdefghijkmnpqrstuvwxyz", digits = "23456789";
        const string all = upper + lower + digits;
        var chars = new char[12];
        chars[0] = upper[RandomNumberGenerator.GetInt32(upper.Length)];
        chars[1] = lower[RandomNumberGenerator.GetInt32(lower.Length)];
        chars[2] = digits[RandomNumberGenerator.GetInt32(digits.Length)];
        for (var i = 3; i < chars.Length; i++) chars[i] = all[RandomNumberGenerator.GetInt32(all.Length)];
        for (var i = chars.Length - 1; i > 0; i--) { var j = RandomNumberGenerator.GetInt32(i + 1); (chars[i], chars[j]) = (chars[j], chars[i]); }
        return new string(chars);
    }

    private async Task<bool> TrySendAsync(string to, string subject, string html, CancellationToken ct)
    {
        try { await _email.SendAsync(to, subject, html, ct); return true; }
        catch (Exception ex) { _log.LogWarning(ex, "Không gửi được email tới {To}", to); return false; }
    }
}
