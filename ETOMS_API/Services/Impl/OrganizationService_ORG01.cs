using ETOMS_API.DTOs.Organization;
using ETOMS_API.Models;
using ETOMS_API.Repositories.Interface;
using ETOMS_API.Services.Interface;
using ETOMS_API.Utils;
using Microsoft.EntityFrameworkCore;

namespace ETOMS_API.Services.Impl;

public sealed partial class OrganizationService
{
        public async Task<Result<OrganizationCreatedResponse>> CreateAsync(
            CreateOrganizationRequest request,
            CancellationToken cancellationToken)
        {
            var type = request.OrganizationType.Trim().ToUpperInvariant();
            if (!OrganizationTypes.Contains(type))
            {
                return Result<OrganizationCreatedResponse>.FieldFailure(
                    nameof(request.OrganizationType),
                    "Loại đơn vị không hợp lệ.");
            }

            var creatorExists = await _unitOfWork.Repository<Account>().Query()
                .AnyAsync(account => account.AccountId == request.CreatedByAccountId, cancellationToken);
            if (!creatorExists)
            {
                return Result.Failure<OrganizationCreatedResponse>("Không tìm thấy tài khoản đăng ký.", StatusCodes.Status404NotFound);
            }

            var organization = new Organization
            {
                Name = request.Name.Trim(),
                OrganizationType = type,
                ParentOrganization = NormalizeOptional(request.ParentOrganization),
                ContactEmail = NormalizeOptional(request.ContactEmail),
                ContactPhone = NormalizeOptional(request.ContactPhone),
                WebsiteUrl = NormalizeOptional(request.WebsiteUrl),
                LogoUrl = NormalizeOptional(request.LogoUrl),
                Description = NormalizeOptional(request.Description),
                Status = "PENDING",
                CreatedByAccountId = request.CreatedByAccountId
            };
            var chair = new OrganizationMember
            {
                AccountId = request.CreatedByAccountId,
                Role = "CHAIR",
                Status = "ACTIVE",
                JoinedAt = DateTime.UtcNow
            };
            organization.OrganizationMembers.Add(chair);
            _unitOfWork.Repository<Organization>().Add(organization);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Result.Success(
                new OrganizationCreatedResponse(
                    organization.OrganizationId,
                    organization.Status,
                    OrganizationStatusLabel(organization.Status)),
                "Đã gửi hồ sơ đăng ký đơn vị tổ chức.");
        }

        public async Task<Result<OrganizationDocumentResponse>> UploadDocumentAsync(
            int organizationId,
            string documentType,
            IFormFile file,
            CancellationToken cancellationToken)
        {
            var organization = await FindOrganizationAsync(organizationId, cancellationToken);
            if (organization is null)
            {
                return Result.Failure<OrganizationDocumentResponse>("Không tìm thấy đơn vị tổ chức.", StatusCodes.Status404NotFound);
            }

            if (organization.Status != "PENDING")
            {
                return Result.Failure<OrganizationDocumentResponse>("Chỉ hồ sơ đang chờ xét duyệt mới được bổ sung giấy tờ.");
            }

            var normalizedType = documentType.Trim();
            if (normalizedType.Length == 0)
            {
                return Result<OrganizationDocumentResponse>.FieldFailure(nameof(documentType), "Loại giấy tờ không được để trống.");
            }

            StoredPrivateFile storedFile;
            try
            {
                storedFile = await _fileStorage.SaveAsync(file, "organization-documents", cancellationToken);
            }
            catch (InvalidDataException exception)
            {
                return Result<OrganizationDocumentResponse>.FieldFailure(nameof(file), exception.Message);
            }

            var document = new OrganizationDocument
            {
                OrganizationId = organizationId,
                DocumentType = normalizedType,
                FileUrl = storedFile.RelativePath,
                OriginalFileName = storedFile.OriginalFileName
            };
            _unitOfWork.Repository<OrganizationDocument>().Add(document);

            try
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
            catch
            {
                await _fileStorage.DeleteAsync(storedFile.RelativePath);
                throw;
            }

            return Result.Success(ToDocumentResponse(document), "Tải giấy tờ đơn vị thành công.");
        }
}
