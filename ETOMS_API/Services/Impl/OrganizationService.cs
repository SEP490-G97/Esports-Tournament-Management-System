using ETOMS_API.DTOs.Organization;
using ETOMS_API.Models;
using ETOMS_API.Repositories.Interface;
using ETOMS_API.Services.Interface;
using ETOMS_API.Utils;
using Microsoft.EntityFrameworkCore;

namespace ETOMS_API.Services.Impl;

public sealed partial class OrganizationService : IOrganizationService
{
    private static readonly HashSet<string> OrganizationTypes = new(StringComparer.Ordinal)
    {
        "UNIVERSITY_CLUB",
        "EVENT_COMPANY",
        "INDIVIDUAL"
    };

    private static readonly HashSet<string> MemberRoles = new(StringComparer.Ordinal)
    {
        "CHAIR",
        "TOURNAMENT_OPERATOR",
        "MARKETING"
    };

    private readonly IUnitOfWork _unitOfWork;
    private readonly PrivateFileStorage _fileStorage;

    public OrganizationService(IUnitOfWork unitOfWork, PrivateFileStorage fileStorage)
    {
        _unitOfWork = unitOfWork;
        _fileStorage = fileStorage;
    }































    private async Task<Organization?> FindOrganizationAsync(int organizationId, CancellationToken cancellationToken)
        => await _unitOfWork.Repository<Organization>().Query()
            .FirstOrDefaultAsync(item => item.OrganizationId == organizationId, cancellationToken);

    private static System.Linq.Expressions.Expression<Func<OrganizationMember, OrganizationMemberResponse>> ToMemberProjection()
        => member => new OrganizationMemberResponse(
            member.OrganizationMemberId,
            member.AccountId,
            member.Account.FullName,
            member.Role,
            MemberRoleLabel(member.Role),
            member.Status,
            MemberStatusLabel(member.Status),
            member.InvitedAt,
            member.JoinedAt);

    private static string OrganizationTypeLabel(string type) => type switch
    {
        "UNIVERSITY_CLUB" => "Câu lạc bộ cấp trường",
        "EVENT_COMPANY" => "Công ty tổ chức sự kiện",
        "INDIVIDUAL" => "Cá nhân",
        _ => "Không xác định"
    };

    private static string OrganizationStatusLabel(string status) => status switch
    {
        "PENDING" => "Chờ xét duyệt",
        "ACTIVE" => "Đang hoạt động",
        "REJECTED" => "Bị từ chối",
        "SUSPENDED" => "Bị đình chỉ",
        _ => "Không xác định"
    };

    private static string MemberRoleLabel(string role) => role switch
    {
        "CHAIR" => "Chủ nhiệm đơn vị",
        "TOURNAMENT_OPERATOR" => "Điều hành giải",
        "MARKETING" => "Truyền thông",
        _ => "Không xác định"
    };

    private static string MemberStatusLabel(string status) => status switch
    {
        "INVITED" => "Đã gửi lời mời",
        "ACTIVE" => "Đang hoạt động",
        "REMOVED" => "Đã rời đơn vị",
        _ => "Không xác định"
    };

    private static string TournamentStatusLabel(string status) => status switch
    {
        "DRAFT" => "Bản nháp",
        "PUBLISHED" => "Đã công bố",
        "ONGOING" => "Đang diễn ra",
        "COMPLETED" => "Đã kết thúc",
        "CANCELLED" => "Đã hủy",
        _ => "Không xác định"
    };

    private static OrganizationDocumentResponse ToDocumentResponse(OrganizationDocument document)
        => new(
            document.OrganizationDocumentId,
            document.OrganizationId,
            document.DocumentType,
            document.OriginalFileName,
            document.UploadedAt);

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
