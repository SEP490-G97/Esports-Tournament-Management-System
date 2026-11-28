namespace ETOMS_API.DTOs.Admin;

// UC-43: tuyển thủ xin làm Ban tổ chức -> Admin cấp một tài khoản BTC RIÊNG (email khác, không có đội).
public sealed class SubmitOrganizerRequest
{
    // Mong muốn / lý do muốn làm Ban tổ chức (bắt buộc, 20–1000 ký tự).
    public string? Note { get; set; }
    // Email đề xuất cho tài khoản BTC (không bắt buộc) — Admin có thể dùng hoặc đổi.
    public string? ProposedEmail { get; set; }
}

public sealed class MyOrganizerRequestDto
{
    public string Role { get; set; } = string.Empty;
    public string Status { get; set; } = "NONE";        // NONE | PENDING | APPROVED | REJECTED
    public string StatusLabel { get; set; } = string.Empty;
    public DateTime? RequestedAt { get; set; }
    public string? Note { get; set; }
    public string? ProposedEmail { get; set; }
    public bool CanSubmit { get; set; }
    // Đơn đã duyệt: email tài khoản Ban tổ chức được cấp và trạng thái mật khẩu (góp ý 28/11):
    // AVAILABLE = còn một lần xem, REVEALED = đã xem (không hiện lại), REISSUE_PENDING = đã xin xem lại, chờ Admin xác nhận.
    public string? OrganizerEmail { get; set; }
    public string? PasswordState { get; set; }
}

// Mật khẩu tài khoản Ban tổ chức — trả về đúng một lần khi người xin bấm xem.
public sealed record RevealedOrganizerPassword(string Email, string Password);

// Yêu cầu xem lại mật khẩu Ban tổ chức đang chờ Admin xác nhận.
public sealed record OrganizerReissueItemDto(int AccountId, string FullName, string Email, string? AvatarUrl, string? OrganizerEmail, DateTime RequestedAt);

public sealed class OrganizerRequestItemDto
{
    public int AccountId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public DateTime RequestedAt { get; set; }
    public string? Note { get; set; }
    public string? ProposedEmail { get; set; }
    public int ActiveTeams { get; set; }
}

public sealed class ApproveOrganizerRequest
{
    // Email đăng nhập của tài khoản BTC mới (bắt buộc, khác email tài khoản tuyển thủ, chưa ai dùng).
    public string? OrganizerEmail { get; set; }
    // Tên hiển thị của tài khoản BTC (không bắt buộc; mặc định "<tên> (BTC)").
    public string? OrganizerFullName { get; set; }
}

public sealed class RejectOrganizerRequest
{
    public string? Reason { get; set; }
}

public sealed class ApproveOrganizerResult
{
    public int OrganizerAccountId { get; set; }
    public string OrganizerEmail { get; set; } = string.Empty;
    // Không còn trả mật khẩu cho Admin (góp ý 28/11): người xin xem mật khẩu một lần ở hồ sơ của họ.
    public bool EmailSent { get; set; }
}
