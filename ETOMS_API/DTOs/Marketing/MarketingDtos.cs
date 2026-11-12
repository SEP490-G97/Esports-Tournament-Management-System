using System.ComponentModel.DataAnnotations;

namespace ETOMS_API.DTOs.Marketing;

/// <summary>Thông tin công khai dùng cho trang giới thiệu một giải đấu.</summary>
public sealed record TournamentLandingResponse(
    int TournamentId,
    string Name,
    string GameName,
    string GameShortCode,
    string? Description,
    string? BannerUrl,
    string? OrganizingUnit,
    string? Region,
    string? ContactInfo,
    bool IsOnline,
    string? Venue,
    string Format,
    DateTimeOffset StartAt,
    DateTimeOffset RegCloseAt,
    decimal EntryFee,
    string? PrizeStructure,
    string Status,
    int RegisteredTeams,
    int MaxTeams,
    bool RegistrationOpen,
    IReadOnlyList<TournamentSponsorResponse> Sponsors);

/// <summary>Nhà tài trợ được công bố trên trang giới thiệu giải.</summary>
public sealed record TournamentSponsorResponse(
    string Name,
    string? LogoUrl,
    string? WebsiteUrl,
    string Tier);

/// <summary>Payload tạo một mã giới thiệu riêng cho cộng tác viên.</summary>
public sealed record CreatePromotionLinkRequest
{
    [Required, StringLength(150, MinimumLength = 1)]
    public string Label { get; init; } = string.Empty;
}

/// <summary>Liên kết cộng tác viên và số liệu đăng ký được ghi nhận.</summary>
public sealed record PromotionLinkResponse(
    int PromotionLinkId,
    int TournamentId,
    string Code,
    string Label,
    int VisitCount,
    int SuccessfulRegistrations,
    string ShareUrl,
    DateTimeOffset CreatedAt);

/// <summary>Giải đấu mà ban tổ chức hiện tại có thể quản lý nội dung truyền thông.</summary>
public sealed record MarketingTournamentResponse(
    int TournamentId,
    string Name,
    string GameName,
    string Status,
    DateTimeOffset StartAt);
