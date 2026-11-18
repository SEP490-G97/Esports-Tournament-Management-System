namespace ETOMS_API.DTOs.Admin
{
    public sealed class AccountListQuery
    {
        /// <summary>Tìm theo tên hoặc email (chứa chuỗi, không phân biệt hoa thường).</summary>
        public string? Q { get; set; }

        /// <summary>PLAYER | ORGANIZER | ADMIN. Rỗng = tất cả.</summary>
        public string? Role { get; set; }

        /// <summary>ACTIVE | BANNED | UNVERIFIED (hoặc PENDING_VERIFY). Rỗng/ALL = tất cả.</summary>
        public string? Status { get; set; }

        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }

    public sealed record AccountListItem(
        int AccountId,
        string Email,
        string FullName,
        string? AvatarUrl,
        string Role,
        string Status,
        string? BanReason,
        bool HasPassword,
        bool HasGoogle,
        DateTime? EmailVerifiedAt,
        DateTime CreatedAt);

    /// <summary>Số tài khoản theo trạng thái, tính trên bộ lọc tìm kiếm + role (không áp bộ lọc trạng thái) để hiện trên các tab.</summary>
    public sealed record AccountStatusCounts(int All, int Active, int Banned, int Unverified);

    public sealed record AccountListResponse(
        IReadOnlyList<AccountListItem> Items,
        int Total,
        int Page,
        int PageSize,
        AccountStatusCounts Counts);

    // Handovers: bàn giao chủ nhiệm các đơn vị người bị khóa đang giữ (Admin chọn người nhận).
    // ConfirmWithoutHandover: Admin đã được cảnh báo và vẫn khóa mà không bàn giao (đơn vị tạm không có chủ nhiệm).
    public sealed record BanAccountRequest(string? Reason, IReadOnlyList<ChairHandover>? Handovers = null, bool ConfirmWithoutHandover = false);

    public sealed record ChairHandover(int OrganizationId, int NewChairAccountId);

    /// <summary>Ảnh hưởng nếu khóa tài khoản: các đơn vị đang làm chủ nhiệm + người có thể nhận bàn giao.</summary>
    public sealed record BanImpactResponse(IReadOnlyList<BanImpactOrganization> ChairedOrganizations, int OpenTournamentCount);

    public sealed record BanImpactOrganization(int OrganizationId, string Name, int OpenTournamentCount, IReadOnlyList<BanImpactCandidate> Candidates);

    public sealed record BanImpactCandidate(int AccountId, string FullName, string Email, string OrgRoleLabel);

    public sealed record HandoverReminderRequest(string? Reason);

    public sealed record UnbanAccountRequest(string? Note);
}
