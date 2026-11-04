using ETOMS_API.DTOs.Organization;
using ETOMS_API.Models;
using ETOMS_API.Repositories.Interface;
using ETOMS_API.Services.Interface;
using ETOMS_API.Utils;
using Microsoft.EntityFrameworkCore;

namespace ETOMS_API.Services.Impl;

public sealed partial class OrganizationService
{
        public async Task<Result> TransferChairAsync(
            int organizationId,
            int newChairAccountId,
            CancellationToken cancellationToken)
        {
            var organizationExists = await _unitOfWork.Repository<Organization>().Query()
                .AnyAsync(item => item.OrganizationID == organizationId && item.Status == "ACTIVE", cancellationToken);
            if (!organizationExists)
            {
                return Result.Failure("Không tìm thấy đơn vị đang hoạt động.", StatusCodes.Status404NotFound);
            }

            var members = _unitOfWork.Repository<OrganizationMember>();
            var currentChair = await members.Query().FirstOrDefaultAsync(
                item => item.OrganizationID == organizationId && item.Role == "CHAIR" && item.Status == "ACTIVE",
                cancellationToken);
            var nextChair = await members.Query().FirstOrDefaultAsync(
                item => item.OrganizationID == organizationId
                    && item.AccountID == newChairAccountId
                    && item.Status == "ACTIVE",
                cancellationToken);
            if (currentChair is null || nextChair is null)
            {
                return Result.Failure("Chủ nhiệm hiện tại hoặc thành viên nhận bàn giao không hợp lệ.", StatusCodes.Status409Conflict);
            }

            if (currentChair.OrganizationMemberID == nextChair.OrganizationMemberID)
            {
                return Result.Failure("Tài khoản này hiện đã là chủ nhiệm.", StatusCodes.Status409Conflict);
            }

            currentChair.Role = "TOURNAMENT_OPERATOR";
            nextChair.Role = "CHAIR";
            members.Update(currentChair);
            members.Update(nextChair);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Result.Success("Đã bàn giao quyền chủ nhiệm đơn vị.");
        }
}
