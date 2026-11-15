using ETOMS_API.DTOs.Organization;
using ETOMS_API.Services.Interface;
using Microsoft.AspNetCore.Mvc;

namespace ETOMS_API.Controllers;

[Route("api/organizations")]
public sealed class OrganizationsController : ApiControllerBase
{
    private readonly IOrganizationService _organizationService;

    public OrganizationsController(IOrganizationService organizationService)
    {
        _organizationService = organizationService;
    }

    [HttpGet]
    public async Task<IActionResult> GetPublicOrganizations(CancellationToken cancellationToken)
        => FromResult(await _organizationService.GetPublicOrganizationsAsync(cancellationToken));

    [HttpGet("{organizationId:int}")]
    public async Task<IActionResult> GetPublicProfile(int organizationId, CancellationToken cancellationToken)
        => FromResult(await _organizationService.GetPublicProfileAsync(organizationId, cancellationToken));

    [HttpPost]
    public async Task<IActionResult> CreateOrganization(
        [FromBody] CreateOrganizationRequest request,
        CancellationToken cancellationToken)
        => FromResult(await _organizationService.CreateAsync(request, cancellationToken));

    [HttpPost("{organizationId:int}/documents")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadDocument(
        int organizationId,
        [FromForm] string documentType,
        [FromForm] IFormFile file,
        CancellationToken cancellationToken)
        => FromResult(await _organizationService.UploadDocumentAsync(
            organizationId, documentType, file, cancellationToken));

    [HttpGet("review-queue")]
    public async Task<IActionResult> GetReviewQueue(CancellationToken cancellationToken)
        => FromResult(await _organizationService.GetReviewQueueAsync(cancellationToken));

    [HttpPost("{organizationId:int}/review")]
    public async Task<IActionResult> ReviewOrganization(
        int organizationId,
        [FromBody] ReviewOrganizationRequest request,
        CancellationToken cancellationToken)
        => FromResult(await _organizationService.ReviewAsync(organizationId, request, cancellationToken));

    [HttpGet("{organizationId:int}/members")]
    public async Task<IActionResult> GetMembers(int organizationId, CancellationToken cancellationToken)
        => FromResult(await _organizationService.GetMembersAsync(organizationId, cancellationToken));

    [HttpPost("{organizationId:int}/members")]
    public async Task<IActionResult> InviteMember(
        int organizationId,
        [FromBody] InviteOrganizationMemberRequest request,
        CancellationToken cancellationToken)
        => FromResult(await _organizationService.InviteMemberAsync(organizationId, request, cancellationToken));

    [HttpPost("{organizationId:int}/members/{memberId:int}/accept")]
    public async Task<IActionResult> AcceptInvitation(
        int organizationId,
        int memberId,
        CancellationToken cancellationToken)
        => FromResult(await _organizationService.AcceptInvitationAsync(
            organizationId, memberId, cancellationToken));

    [HttpPatch("{organizationId:int}/members/{memberId:int}")]
    public async Task<IActionResult> UpdateMemberRole(
        int organizationId,
        int memberId,
        [FromBody] UpdateOrganizationMemberRoleRequest request,
        CancellationToken cancellationToken)
        => FromResult(await _organizationService.UpdateMemberRoleAsync(
            organizationId, memberId, request.Role, cancellationToken));

    [HttpPost("{organizationId:int}/chair-transfer")]
    public async Task<IActionResult> TransferChair(
        int organizationId,
        [FromBody] TransferOrganizationChairRequest request,
        CancellationToken cancellationToken)
        => FromResult(await _organizationService.TransferChairAsync(
            organizationId, request.NewChairAccountId, cancellationToken));

    [HttpGet("{organizationId:int}/reputation")]
    public async Task<IActionResult> GetReputation(int organizationId, CancellationToken cancellationToken)
        => FromResult(await _organizationService.GetReputationAsync(organizationId, cancellationToken));

    [HttpPost("{organizationId:int}/suspension")]
    public async Task<IActionResult> SuspendOrganization(
        int organizationId,
        [FromBody] SuspendOrganizationRequest request,
        CancellationToken cancellationToken)
        => FromResult(await _organizationService.SuspendAsync(organizationId, request.Reason, cancellationToken));
}
