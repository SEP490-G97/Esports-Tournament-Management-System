using System.ComponentModel.DataAnnotations;

namespace ETOMS_API.DTOs.Organization;

public sealed class CreateOrganizationRequest
{
    [Required, StringLength(150)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public string OrganizationType { get; set; } = string.Empty;

    [StringLength(150)]
    public string? ParentOrganization { get; set; }

    [EmailAddress, StringLength(256)]
    public string? ContactEmail { get; set; }

    [StringLength(20)]
    public string? ContactPhone { get; set; }

    [Url, StringLength(500)]
    public string? WebsiteUrl { get; set; }

    [StringLength(500)]
    public string? LogoUrl { get; set; }

    [StringLength(1000)]
    public string? Description { get; set; }
}

public sealed class ReviewOrganizationRequest
{
    [Required]
    public string Decision { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Reason { get; set; }
}

public sealed class InviteOrganizationMemberRequest
{
    [Range(1, int.MaxValue)]
    public int AccountId { get; set; }

    [Required]
    public string Role { get; set; } = string.Empty;
}

public sealed class UpdateOrganizationMemberRoleRequest
{
    [Required]
    public string Role { get; set; } = string.Empty;
}

public sealed class TransferOrganizationChairRequest
{
    [Range(1, int.MaxValue)]
    public int NewChairAccountId { get; set; }
}

public sealed class SuspendOrganizationRequest
{
    [Required, StringLength(1000)]
    public string Reason { get; set; } = string.Empty;
}

public sealed record OrganizationCreatedResponse(
    int OrganizationId,
    string Status,
    string StatusLabel,
    IReadOnlyList<string> RequiredDocumentTypes);

public sealed record OrganizationDocumentResponse(
    int DocumentId,
    int OrganizationId,
    string DocumentType,
    string OriginalFileName,
    DateTime UploadedAt);

public sealed record OrganizationMemberResponse(
    int OrganizationMemberId,
    int AccountId,
    string FullName,
    string Role,
    string RoleLabel,
    string Status,
    string StatusLabel,
    DateTime InvitedAt,
    DateTime? JoinedAt);

public sealed record OrganizationTournamentResponse(
    int TournamentId,
    string Name,
    string Status,
    string StatusLabel,
    DateTime StartAt);

public sealed record OrganizationReputationResponse(
    int OrganizationId,
    int CompletedTournamentCount,
    int TotalTournamentCount,
    decimal CompletionRate,
    int DisputedMatchCount,
    int TotalMatchCount,
    decimal DisputeRate,
    double? AverageDisputeResolutionHours,
    int RecordedRefundCount);

public sealed record PublicOrganizationResponse(
    int OrganizationId,
    string Name,
    string OrganizationType,
    string OrganizationTypeLabel,
    string? ParentOrganization,
    string? ContactEmail,
    string? ContactPhone,
    string? WebsiteUrl,
    string? LogoUrl,
    string? Description,
    IReadOnlyList<OrganizationTournamentResponse> Tournaments,
    OrganizationReputationResponse Reputation);

public sealed record OrganizationReviewResponse(
    int OrganizationId,
    string Name,
    string OrganizationType,
    string Status,
    string StatusLabel,
    string? ReviewReason,
    DateTime CreatedAt,
    IReadOnlyList<string> RequiredDocumentTypes,
    IReadOnlyList<OrganizationDocumentResponse> Documents);
