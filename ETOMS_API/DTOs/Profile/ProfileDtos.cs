using System.ComponentModel.DataAnnotations;

namespace ETOMS_API.DTOs.Profile;

public sealed class UpdateProfileRequest
{
    [Required(ErrorMessage = "Vui lòng nhập tên hiển thị.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Tên hiển thị phải từ 2 đến 100 ký tự.")]
    public string FullName { get; set; } = string.Empty;

    [StringLength(500, ErrorMessage = "Liên kết ảnh đại diện tối đa 500 ký tự — hãy dùng nút chọn ảnh từ máy.")]
    public string? AvatarUrl { get; set; }

    public DateOnly? DateOfBirth { get; set; }

    [StringLength(20, ErrorMessage = "Số điện thoại tối đa 20 ký tự.")]
    [RegularExpression(@"^0\d{9,10}$", ErrorMessage = "Số điện thoại không hợp lệ (phải bắt đầu bằng 0, 10-11 chữ số).")]
    public string? Phone { get; set; }

    [StringLength(500, ErrorMessage = "Liên kết mạng xã hội tối đa 500 ký tự.")]
    public string? SocialLinks { get; set; }
}

public sealed class UpsertGameAccountRequest
{
    [Required(ErrorMessage = "Vui lòng nhập tên trong game.")]
    [StringLength(150, MinimumLength = 2, ErrorMessage = "Tên trong game phải từ 2 đến 150 ký tự.")]
    public string InGameName { get; set; } = string.Empty;
}

public sealed class UpdateNotificationPreferencesRequest
{
    public bool NotifyInApp { get; set; }
    public bool NotifyEmail { get; set; }
}

public sealed record ProfileResponse(
    int AccountId,
    string Email,
    string FullName,
    string? AvatarUrl,
    DateOnly? DateOfBirth,
    string? Phone,
    string? SocialLinks);

public sealed record CredentialResponse(
    int CredentialId,
    int? GameId,
    string CredentialType,
    string CredentialTypeLabel,
    string? Value,
    DateTime CreatedAt);

public sealed record IdentityDocumentDownload(string RelativePath, string FileName);

public sealed record NotificationPreferencesResponse(bool NotifyInApp, bool NotifyEmail);

public sealed record PublicProfileResponse(
    int AccountId,
    string FullName,
    string? AvatarUrl,
    IReadOnlyList<PublicGameAccountResponse> GameAccounts,
    IReadOnlyList<PublicTeamResponse> Teams,
    IReadOnlyList<PublicTitleResponse> Titles);

public sealed record PublicGameAccountResponse(int GameId, string GameTitle, string InGameName);
public sealed record PublicTeamResponse(int TeamId, string TeamName, string GameTitle);
public sealed record PublicTitleResponse(
    int TournamentId,
    string TournamentName,
    string Placement,
    string PlacementLabel);

public sealed record MatchHistoryResponse(
    int MatchId,
    string TournamentName,
    string TeamName,
    string Slot,
    DateTime? ScheduledAt,
    int? ScoreFor,
    int? ScoreAgainst,
    string Status,
    string StatusLabel);
