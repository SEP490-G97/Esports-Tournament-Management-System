using System.ComponentModel.DataAnnotations;

namespace ETOMS_API.DTOs.Registration;

public sealed record RegistrationLineupRequest
{
    [Range(1, int.MaxValue)]
    public int AccountId { get; init; }

    [Required, StringLength(20)]
    public string Slot { get; init; } = string.Empty;
}

public sealed record CreateRegistrationRequest
{
    [Range(1, int.MaxValue)]
    public int TeamId { get; init; }

    [Required]
    public IReadOnlyList<RegistrationLineupRequest> Lineup { get; init; } = [];

    public bool AcceptRulebook { get; init; }
}

public sealed record ReviewRegistrationRequest
{
    [Required, StringLength(20)]
    public string Decision { get; init; } = string.Empty;

    [StringLength(500)]
    public string? Reason { get; init; }
}

public sealed record VerifyRegistrationPaymentRequest
{
    [Required, StringLength(20)]
    public string Status { get; init; } = string.Empty;

    [StringLength(500)]
    public string? Note { get; init; }
}

public sealed record UploadPaymentProofRequest
{
    [Range(0, double.MaxValue)]
    public decimal? DeclaredAmount { get; init; }
}

public sealed record RefundRegistrationRequest
{
    [Range(0, double.MaxValue)]
    public decimal? Amount { get; init; }

    [Required, StringLength(500)]
    public string Note { get; init; } = string.Empty;
}

public sealed record SetTieBreakerOrderRequest
{
    [Required]
    public IReadOnlyList<string> Order { get; init; } = [];
}

public sealed record RegistrationRosterMemberResponse(
    int AccountId,
    string FullName,
    string TeamSlot,
    string? InGameName,
    bool HasIdentityDocument);

public sealed record RegistrationTeamOptionResponse(
    int TeamId,
    string TeamName,
    string TeamTag,
    IReadOnlyList<RegistrationRosterMemberResponse> Members);

public sealed record RegistrationOptionsResponse(
    int TournamentId,
    string TournamentName,
    string GameName,
    DateTime RegOpenAt,
    DateTime RegCloseAt,
    int MaxTeams,
    int StarterCount,
    int MaxSubstitutes,
    bool RequireInGameName,
    bool RequireIdentityDocument,
    bool RequirePaymentProof,
    decimal EntryFee,
    int RulebookVersion,
    string? RulebookContent,
    int AvailableSlots,
    IReadOnlyList<RegistrationTeamOptionResponse> Teams);

public sealed record RegistrationLineupResponse(
    int AccountId,
    string FullName,
    string Slot,
    string? InGameName,
    IReadOnlyList<int> IdentityDocumentCredentialIds);

public sealed record PaymentRecordResponse(
    int PaymentRecordId,
    string RecordKind,
    string? FileName,
    decimal? DeclaredAmount,
    string? Note,
    DateTime RecordedAt,
    int RecordedByAccountId,
    bool IsCurrent,
    string? DownloadUrl);

public sealed record RegistrationResponse(
    int RegistrationId,
    int TournamentId,
    string TournamentName,
    int TeamId,
    string TeamName,
    string TeamTag,
    decimal EntryFee,
    bool RequirePaymentProof,
    DateTime? SubmittedAt,
    string Status,
    int? WaitlistPosition,
    int WaitlistAheadCount,
    DateTime? WaitlistExpiresAt,
    string PaymentStatus,
    int? RulebookVersionAccepted,
    DateTime? ConsentAt,
    string? ReviewNote,
    DateTime? ReviewedAt,
    IReadOnlyList<RegistrationLineupResponse> Lineup,
    IReadOnlyList<PaymentRecordResponse> PaymentRecords);

public sealed record TeamDisciplineResponse(
    int PenaltyId,
    string TournamentName,
    string Severity,
    int Points,
    string Reason,
    string Status,
    DateTime IssuedAt);

public sealed record RegistrationReviewResponse(
    RegistrationResponse Registration,
    IReadOnlyList<TeamDisciplineResponse> DisciplineHistory);

public sealed record RegistrationListResponse(
    string TournamentName,
    IReadOnlyList<RegistrationResponse> Registrations,
    int MaxTeams,
    int OccupiedSlots,
    int AvailableSlots,
    int WaitlistCount,
    IReadOnlyList<string> TieBreakerOrder);

public sealed record WaitlistPromotionResponse(
    int RegistrationId,
    int TournamentId,
    int? WaitlistPosition,
    DateTime? ConfirmationExpiresAt);

public sealed record PrivateRegistrationFileResponse(string RelativePath, string FileName);

public static class TieBreakerCriteria
{
    public const string HeadToHead = "HEAD_TO_HEAD";
    public const string GameDifference = "GAME_DIFFERENCE";
    public const string TotalGamesWon = "TOTAL_GAMES_WON";
    public const string RandomDraw = "RANDOM_DRAW";

    public static readonly string[] DefaultOrder =
    [
        HeadToHead,
        GameDifference,
        TotalGamesWon,
        RandomDraw
    ];

    public static string Label(string criterion) => criterion switch
    {
        HeadToHead => "Đối đầu trực tiếp",
        GameDifference => "Hiệu số ván",
        TotalGamesWon => "Tổng ván thắng",
        RandomDraw => "Bốc thăm",
        _ => criterion
    };
}
