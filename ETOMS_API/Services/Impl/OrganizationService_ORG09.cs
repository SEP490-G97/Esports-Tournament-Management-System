using ETOMS_API.DTOs.Organization;
using ETOMS_API.Models;
using ETOMS_API.Repositories.Interface;
using ETOMS_API.Services.Interface;
using ETOMS_API.Utils;
using Microsoft.EntityFrameworkCore;

namespace ETOMS_API.Services.Impl;

public sealed partial class OrganizationService
{
        public async Task<Result> SuspendAsync(
            int organizationId,
            string reason,
            CancellationToken cancellationToken)
        {
            var organization = await FindOrganizationAsync(organizationId, cancellationToken);
            if (organization is null)
            {
                return Result.Failure("Không tìm thấy đơn vị tổ chức.", StatusCodes.Status404NotFound);
            }

            if (organization.Status != "ACTIVE")
            {
                return Result.Failure("Chỉ đơn vị đang hoạt động mới có thể bị đình chỉ.", StatusCodes.Status409Conflict);
            }

            var normalizedReason = reason.Trim();
            if (normalizedReason.Length == 0)
            {
                return Result.FieldFailure(nameof(reason), "Lý do đình chỉ không được để trống.");
            }

            organization.Status = "SUSPENDED";
            organization.SuspensionReason = normalizedReason;
            organization.SuspendedAt = DateTime.UtcNow;
            _unitOfWork.Repository<Organization>().Update(organization);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Result.Success("Đã đình chỉ đơn vị tổ chức.");
        }
}
