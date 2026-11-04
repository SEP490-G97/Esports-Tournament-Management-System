using ETOMS_API.DTOs.Organization;
using ETOMS_API.Models;
using ETOMS_API.Repositories.Interface;
using ETOMS_API.Services.Interface;
using ETOMS_API.Utils;
using Microsoft.EntityFrameworkCore;

namespace ETOMS_API.Services.Impl;

public sealed partial class OrganizationService
{
        public async Task<Result<OrganizationMemberResponse>> UpdateMemberRoleAsync(
            int organizationId,
            int memberId,
            string role,
            CancellationToken cancellationToken)
        {
            var normalizedRole = role.Trim().ToUpperInvariant();
            if (normalizedRole is not ("TOURNAMENT_OPERATOR" or "MARKETING"))
            {
                return Result<OrganizationMemberResponse>.FieldFailure(nameof(role), "Vai trò mới chỉ được là TOURNAMENT_OPERATOR hoặc MARKETING.");
            }

            var member = await _unitOfWork.Repository<OrganizationMember>().Query()
                .FirstOrDefaultAsync(item => item.OrganizationID == organizationId
                    && item.OrganizationMemberID == memberId
                    && item.Status != "REMOVED", cancellationToken);
            if (member is null)
            {
                return Result.Failure<OrganizationMemberResponse>("Không tìm thấy thành viên của đơn vị.", StatusCodes.Status404NotFound);
            }

            member.Role = normalizedRole;
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
                "Cập nhật vai trò thành viên thành công.");
        }
}
