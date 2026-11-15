using ETOMS_API.DTOs.Organization;
using ETOMS_API.Utils;

namespace ETOMS_API.Services.Interface;

public interface IOrganizationService
{
    Task<Result<OrganizationCreatedResponse>> CreateAsync(CreateOrganizationRequest request, CancellationToken cancellationToken);
    Task<Result<OrganizationDocumentResponse>> UploadDocumentAsync(int organizationId, string documentType, IFormFile file, CancellationToken cancellationToken);
    Task<Result<IReadOnlyList<OrganizationReviewResponse>>> GetReviewQueueAsync(CancellationToken cancellationToken);
    Task<Result<OrganizationReviewResponse>> ReviewAsync(int organizationId, ReviewOrganizationRequest request, CancellationToken cancellationToken);
    Task<Result<PublicOrganizationResponse>> GetPublicProfileAsync(int organizationId, CancellationToken cancellationToken);
    Task<Result<IReadOnlyList<PublicOrganizationResponse>>> GetPublicOrganizationsAsync(CancellationToken cancellationToken);
    Task<Result<IReadOnlyList<OrganizationMemberResponse>>> GetMembersAsync(int organizationId, CancellationToken cancellationToken);
    Task<Result<OrganizationMemberResponse>> InviteMemberAsync(int organizationId, InviteOrganizationMemberRequest request, CancellationToken cancellationToken);
    Task<Result<OrganizationMemberResponse>> AcceptInvitationAsync(int organizationId, int memberId, CancellationToken cancellationToken);
    Task<Result<OrganizationMemberResponse>> UpdateMemberRoleAsync(int organizationId, int memberId, string role, CancellationToken cancellationToken);
    Task<Result> TransferChairAsync(int organizationId, int newChairAccountId, CancellationToken cancellationToken);
    Task<Result<OrganizationReputationResponse>> GetReputationAsync(int organizationId, CancellationToken cancellationToken);
    Task<Result> SuspendAsync(int organizationId, string reason, CancellationToken cancellationToken);
}
