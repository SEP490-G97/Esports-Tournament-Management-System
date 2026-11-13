using ETOMS_API.DTOs.Organization;
using ETOMS_API.Services.Interface;
using Microsoft.AspNetCore.Mvc;

namespace ETOMS_API.Controllers;

// STUB TẠM thêm kiểm quyền thay module ORG (Cấn Việt Hùng) — bản trước các action đổi dữ liệu
// (duyệt, đình chỉ, mời, đổi chủ nhiệm...) hoàn toàn không kiểm ai gọi, và CreateOrganization
// còn tin thẳng CreatedByAccountId do client tự gửi lên. Không phải bài làm cá nhân của Hùng.
[Route("api/organizations")]
public sealed class OrganizationsController : ApiControllerBase
{
    private readonly IOrganizationService _organizationService;
    private readonly ICurrentUser _currentUser;

    public OrganizationsController(IOrganizationService organizationService, ICurrentUser currentUser)
    {
        _organizationService = organizationService;
        _currentUser = currentUser;
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
    {
        if (!_currentUser.IsAuthenticated) return Unauthorized();
        return FromResult(await _organizationService.CreateAsync(request, _currentUser.UserId!.Value, cancellationToken));
    }

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
    {
        if (!_currentUser.IsAuthenticated || !await _organizationService.IsAdminAsync(_currentUser.UserId!.Value))
            return StatusCode(403, new { message = "Chỉ Admin được xem hàng chờ duyệt." });
        return FromResult(await _organizationService.GetReviewQueueAsync(cancellationToken));
    }

    [HttpPost("{organizationId:int}/review")]
    public async Task<IActionResult> ReviewOrganization(
        int organizationId,
        [FromBody] ReviewOrganizationRequest request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || !await _organizationService.IsAdminAsync(_currentUser.UserId!.Value))
            return StatusCode(403, new { message = "Chỉ Admin được xét duyệt đơn vị tổ chức." });
        return FromResult(await _organizationService.ReviewAsync(organizationId, request, _currentUser.UserId!.Value, cancellationToken));
    }

    [HttpGet("{organizationId:int}/members")]
    public async Task<IActionResult> GetMembers(int organizationId, CancellationToken cancellationToken)
        => FromResult(await _organizationService.GetMembersAsync(organizationId, cancellationToken));

    [HttpPost("{organizationId:int}/members")]
    public async Task<IActionResult> InviteMember(
        int organizationId,
        [FromBody] InviteOrganizationMemberRequest request,
        CancellationToken cancellationToken)
    {
        var denied = await RequireChairOrAdminAsync(organizationId);
        if (denied is not null) return denied;
        return FromResult(await _organizationService.InviteMemberAsync(organizationId, request, cancellationToken));
    }

    [HttpPost("{organizationId:int}/members/{memberId:int}/accept")]
    public async Task<IActionResult> AcceptInvitation(
        int organizationId,
        int memberId,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated) return Unauthorized();
        return FromResult(await _organizationService.AcceptInvitationAsync(
            organizationId, memberId, _currentUser.UserId!.Value, cancellationToken));
    }

    [HttpPatch("{organizationId:int}/members/{memberId:int}")]
    public async Task<IActionResult> UpdateMemberRole(
        int organizationId,
        int memberId,
        [FromBody] UpdateOrganizationMemberRoleRequest request,
        CancellationToken cancellationToken)
    {
        var denied = await RequireChairOrAdminAsync(organizationId);
        if (denied is not null) return denied;
        return FromResult(await _organizationService.UpdateMemberRoleAsync(
            organizationId, memberId, request.Role, cancellationToken));
    }

    [HttpPost("{organizationId:int}/chair-transfer")]
    public async Task<IActionResult> TransferChair(
        int organizationId,
        [FromBody] TransferOrganizationChairRequest request,
        CancellationToken cancellationToken)
    {
        var denied = await RequireChairOrAdminAsync(organizationId);
        if (denied is not null) return denied;
        return FromResult(await _organizationService.TransferChairAsync(
            organizationId, request.NewChairAccountId, cancellationToken));
    }

    [HttpGet("{organizationId:int}/reputation")]
    public async Task<IActionResult> GetReputation(int organizationId, CancellationToken cancellationToken)
        => FromResult(await _organizationService.GetReputationAsync(organizationId, cancellationToken));

    [HttpPost("{organizationId:int}/suspension")]
    public async Task<IActionResult> SuspendOrganization(
        int organizationId,
        [FromBody] SuspendOrganizationRequest request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || !await _organizationService.IsAdminAsync(_currentUser.UserId!.Value))
            return StatusCode(403, new { message = "Chỉ Admin được đình chỉ đơn vị tổ chức." });
        return FromResult(await _organizationService.SuspendAsync(organizationId, request.Reason, cancellationToken));
    }

    private async Task<IActionResult?> RequireChairOrAdminAsync(int organizationId)
    {
        if (!_currentUser.IsAuthenticated) return Unauthorized();
        var uid = _currentUser.UserId!.Value;
        if (await _organizationService.IsActiveChairAsync(organizationId, uid) || await _organizationService.IsAdminAsync(uid))
            return null;
        return StatusCode(403, new { message = "Chỉ chủ nhiệm đơn vị hoặc Admin được thao tác." });
    }
}
