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

    public sealed record BanAccountRequest(string? Reason);

    public sealed record UnbanAccountRequest(string? Note);
}
