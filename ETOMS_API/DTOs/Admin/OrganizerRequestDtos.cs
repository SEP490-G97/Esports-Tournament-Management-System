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
}

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
    // Mật khẩu tạm — chỉ trả cho Admin đúng một lần (và gửi qua email) để bàn giao; BTC đổi ngay khi đăng nhập.
    public string TemporaryPassword { get; set; } = string.Empty;
    public bool EmailSent { get; set; }
}
