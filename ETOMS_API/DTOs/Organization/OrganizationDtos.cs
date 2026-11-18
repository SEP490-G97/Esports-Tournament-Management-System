using System.ComponentModel.DataAnnotations;

namespace ETOMS_API.DTOs.Organization;

public sealed class CreateOrganizationRequest
{
    [Required, StringLength(150)]
    public string Name { get; set; } = string.Empty;

    // Góp ý nhóm 18/11: form không còn hỏi loại hình; bỏ trống thì máy chủ tự gán (xem OrganizationService.DefaultType).
    public string? OrganizationType { get; set; }

    // Email các tài khoản Ban tổ chức cùng lập đơn vị (ngoài người lập). Đơn vị cần tối thiểu 3 Ban tổ chức.
    public List<string>? MemberEmails { get; set; }

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
    // Mời theo Email (giao diện dùng) hoặc AccountId (giữ cho client cũ). Người được mời phải là tài khoản Ban tổ chức.
    [Range(0, int.MaxValue)]
    public int AccountId { get; set; }

    [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
    [StringLength(255)]
    public string? Email { get; set; }

    [Required]
    public string Role { get; set; } = string.Empty;
}

// "Đơn vị của tôi" trong menu tài khoản Ban tổ chức: đơn vị mình là thành viên + lời mời đang chờ.
public sealed record MyOrganizationResponse(
    int OrganizationId,
    string Name,
    string Status,
    string StatusLabel,
    int OrganizationMemberId,
    string Role,
    string RoleLabel,
    string MemberStatus,
    string MemberStatusLabel);

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
