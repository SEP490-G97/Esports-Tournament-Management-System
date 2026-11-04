using ETOMS_API.DTOs.Organization;
using ETOMS_API.Models;
using ETOMS_API.Repositories.Interface;
using ETOMS_API.Services.Interface;
using ETOMS_API.Utils;
using Microsoft.EntityFrameworkCore;

namespace ETOMS_API.Services.Impl;

public sealed partial class OrganizationService
{
        public async Task<Result<IReadOnlyList<OrganizationMemberResponse>>> GetMembersAsync(
            int organizationId,
            CancellationToken cancellationToken)
        {
            var organizationExists = await _unitOfWork.Repository<Organization>().Query()
                .AnyAsync(item => item.OrganizationID == organizationId, cancellationToken);
            if (!organizationExists)
            {
                return Result.Failure<IReadOnlyList<OrganizationMemberResponse>>("Không tìm thấy đơn vị tổ chức.", StatusCodes.Status404NotFound);
            }

            var members = await _unitOfWork.Repository<OrganizationMember>().Query()
                .AsNoTracking()
                .Where(member => member.OrganizationID == organizationId && member.Status != "REMOVED")
                .OrderBy(member => member.Role)
                .ThenBy(member => member.Account.FullName)
                .Select(ToMemberProjection())
                .ToListAsync(cancellationToken);

            return Result.Success<IReadOnlyList<OrganizationMemberResponse>>(members);
        }

        public async Task<Result<OrganizationMemberResponse>> InviteMemberAsync(
            int organizationId,
            InviteOrganizationMemberRequest request,
            CancellationToken cancellationToken)
        {
            var organization = await FindOrganizationAsync(organizationId, cancellationToken);
            if (organization is null)
            {
                return Result.Failure<OrganizationMemberResponse>("Không tìm thấy đơn vị tổ chức.", StatusCodes.Status404NotFound);
            }

            if (organization.Status != "ACTIVE")
            {
                return Result.Failure<OrganizationMemberResponse>("Chỉ đơn vị đang hoạt động mới được mời thành viên.");
            }

            var role = request.Role.Trim().ToUpperInvariant();
            if (!MemberRoles.Contains(role))
            {
                return Result<OrganizationMemberResponse>.FieldFailure(nameof(request.Role), "Vai trò thành viên không hợp lệ.");
            }

            var account = await _unitOfWork.Repository<Account>().Query()
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.AccountID == request.AccountId, cancellationToken);
            if (account is null)
            {
                return Result.Failure<OrganizationMemberResponse>("Không tìm thấy tài khoản được mời.", StatusCodes.Status404NotFound);
            }

            var existing = await _unitOfWork.Repository<OrganizationMember>().Query()
                .AnyAsync(member => member.OrganizationID == organizationId
                    && member.AccountID == request.AccountId
                    && member.Status != "REMOVED", cancellationToken);
            if (existing)
            {
                return Result.Failure<OrganizationMemberResponse>("Tài khoản đã là thành viên hoặc đang có lời mời của đơn vị.");
            }

            var member = new OrganizationMember
            {
                OrganizationID = organizationId,
                AccountID = request.AccountId,
                Role = role,
                Status = "INVITED"
            };
            _unitOfWork.Repository<OrganizationMember>().Add(member);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Result.Success(new OrganizationMemberResponse(
                member.OrganizationMemberID,
                account.AccountID,
                account.FullName,
                member.Role,
                MemberRoleLabel(member.Role),
                member.Status,
                MemberStatusLabel(member.Status),
                member.InvitedAt,
                member.JoinedAt),
                "Đã gửi lời mời tham gia đơn vị.");
        }

        public async Task<Result<OrganizationMemberResponse>> AcceptInvitationAsync(
            int organizationId,
            int memberId,
            CancellationToken cancellationToken)
        {
            var member = await _unitOfWork.Repository<OrganizationMember>().Query()
                .FirstOrDefaultAsync(item => item.OrganizationID == organizationId
                    && item.OrganizationMemberID == memberId
                    && item.Status == "INVITED", cancellationToken);
            if (member is null)
            {
                return Result.Failure<OrganizationMemberResponse>("Không tìm thấy lời mời đang chờ xác nhận.", StatusCodes.Status404NotFound);
            }

            member.Status = "ACTIVE";
            member.JoinedAt = DateTime.UtcNow;
            _unitOfWork.Repository<OrganizationMember>().Update(member);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var account = await _unitOfWork.Repository<Account>().Query()
                .AsNoTracking()
                .FirstAsync(item => item.AccountID == member.AccountID, cancellationToken);
            return Result.Success(new OrganizationMemberResponse(
                member.OrganizationMemberID,
                account.AccountID,
                account.FullName,
                member.Role,
                MemberRoleLabel(member.Role),
                member.Status,
                MemberStatusLabel(member.Status),
                member.InvitedAt,
                member.JoinedAt),
                "Đã chấp nhận lời mời tham gia đơn vị.");
        }
}
