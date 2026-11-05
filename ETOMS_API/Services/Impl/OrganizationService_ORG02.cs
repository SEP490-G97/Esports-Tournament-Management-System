using ETOMS_API.DTOs.Organization;
using ETOMS_API.Models;
using ETOMS_API.Repositories.Interface;
using ETOMS_API.Services.Interface;
using ETOMS_API.Utils;
using Microsoft.EntityFrameworkCore;

namespace ETOMS_API.Services.Impl;

public sealed partial class OrganizationService
{
        public async Task<Result<OrganizationReviewResponse>> ReviewAsync(
            int organizationId,
            ReviewOrganizationRequest request,
            CancellationToken cancellationToken)
        {
            var organization = await FindOrganizationAsync(organizationId, cancellationToken);
            if (organization is null)
            {
                return Result.Failure<OrganizationReviewResponse>("Không tìm thấy đơn vị tổ chức.", StatusCodes.Status404NotFound);
            }

            if (organization.Status != "PENDING")
            {
                return Result.Failure<OrganizationReviewResponse>("Hồ sơ không còn ở trạng thái chờ xét duyệt.", StatusCodes.Status409Conflict);
            }

            var decision = request.Decision.Trim().ToUpperInvariant();
            if (decision is not ("APPROVE" or "REJECT"))
            {
                return Result<OrganizationReviewResponse>.FieldFailure(nameof(request.Decision), "Quyết định chỉ được là APPROVE hoặc REJECT.");
            }

            var reviewerExists = await _unitOfWork.Repository<Account>().Query()
                .AnyAsync(account => account.AccountId == request.ReviewerAccountId, cancellationToken);
            if (!reviewerExists)
            {
                return Result.Failure<OrganizationReviewResponse>("Không tìm thấy tài khoản người xét duyệt.", StatusCodes.Status404NotFound);
            }

            var documents = await _unitOfWork.Repository<OrganizationDocument>().Query()
                .AsNoTracking()
                .Where(document => document.OrganizationId == organizationId)
                .OrderBy(document => document.UploadedAt)
                .ToListAsync(cancellationToken);
            if (documents.Count == 0)
            {
                return Result<OrganizationReviewResponse>.FieldFailure(
                    nameof(request.Decision),
                    "Cần tải ít nhất một giấy tờ trước khi xét duyệt hồ sơ.");
            }

            var reason = NormalizeOptional(request.Reason);
            if (decision == "REJECT" && reason is null)
            {
                return Result<OrganizationReviewResponse>.FieldFailure(nameof(request.Reason), "Từ chối hồ sơ phải ghi rõ lý do.");
            }

            organization.Status = decision == "APPROVE" ? "ACTIVE" : "REJECTED";
            organization.ReviewReason = reason;
            organization.ReviewedByAccountId = request.ReviewerAccountId;
            organization.ReviewedAt = DateTime.UtcNow;
            _unitOfWork.Repository<Organization>().Update(organization);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Result.Success(new OrganizationReviewResponse(
                organization.OrganizationId,
                organization.Name,
                organization.OrganizationType,
                organization.Status,
                OrganizationStatusLabel(organization.Status),
                organization.ReviewReason,
                organization.CreatedAt,
                documents.Select(ToDocumentResponse).ToArray()));
        }

        public async Task<Result<IReadOnlyList<OrganizationReviewResponse>>> GetReviewQueueAsync(
            CancellationToken cancellationToken)
        {
            var organizations = await _unitOfWork.Repository<Organization>().Query()
                .AsNoTracking()
                .Where(item => item.Status == "PENDING")
                .OrderBy(item => item.CreatedAt)
                .ToListAsync(cancellationToken);
            var organizationIds = organizations.Select(item => item.OrganizationId).ToArray();
            var documents = await _unitOfWork.Repository<OrganizationDocument>().Query()
                .AsNoTracking()
                .Where(item => organizationIds.Contains(item.OrganizationId))
                .OrderBy(item => item.UploadedAt)
                .ToListAsync(cancellationToken);
            var documentsByOrganization = documents
                .GroupBy(item => item.OrganizationId)
                .ToDictionary(group => group.Key, group => group.Select(ToDocumentResponse).ToArray());

            var queue = organizations.Select(organization => new OrganizationReviewResponse(
                organization.OrganizationId,
                organization.Name,
                organization.OrganizationType,
                organization.Status,
                OrganizationStatusLabel(organization.Status),
                organization.ReviewReason,
                organization.CreatedAt,
                documentsByOrganization.TryGetValue(organization.OrganizationId, out var organizationDocuments)
                    ? organizationDocuments
                    : []))
                .ToArray();

            return Result.Success<IReadOnlyList<OrganizationReviewResponse>>(queue);
        }
}
