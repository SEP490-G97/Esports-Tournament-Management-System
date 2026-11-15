using System.ComponentModel.DataAnnotations;

namespace ETOMS_API.DTOs.Profile;

public sealed class UpdateProfileRequest
{
    [Required, StringLength(150)]
    public string FullName { get; set; } = string.Empty;

    [StringLength(500)]
    public string? AvatarUrl { get; set; }

    public DateOnly? DateOfBirth { get; set; }

    [StringLength(20)]
    public string? Phone { get; set; }

    [StringLength(500)]
    public string? SocialLinks { get; set; }
}

public sealed class UpsertGameAccountRequest
{
    [Required, StringLength(150)]
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
