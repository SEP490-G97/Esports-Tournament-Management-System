using ETOMS_API.DTOs.Profile;
using ETOMS_API.Models;
using ETOMS_API.Repositories.Interface;
using ETOMS_API.Services.Interface;
using ETOMS_API.Utils;
using Microsoft.EntityFrameworkCore;

namespace ETOMS_API.Services.Impl;

public sealed partial class ProfileService
{
        public async Task<Result<CredentialResponse>> UploadIdentityDocumentAsync(
            int accountId,
            IFormFile file,
            CancellationToken cancellationToken)
        {
            var accountExists = await _unitOfWork.Repository<Account>().Query()
                .AnyAsync(account => account.AccountId == accountId, cancellationToken);
            if (!accountExists)
            {
                return Result.Failure<CredentialResponse>("Không tìm thấy tài khoản.", StatusCodes.Status404NotFound);
            }

            StoredPrivateFile storedFile;
            try
            {
                storedFile = await _fileStorage.SaveAsync(file, "identity-documents", cancellationToken);
            }
            catch (InvalidDataException exception)
            {
                return Result<CredentialResponse>.FieldFailure(nameof(file), exception.Message);
            }

            var credential = new PlayerCredential
            {
                AccountId = accountId,
                CredentialType = IdentityDocumentType,
                FileUrl = storedFile.RelativePath
            };
            _unitOfWork.Repository<PlayerCredential>().Add(credential);

            try
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
            catch
            {
                await _fileStorage.DeleteAsync(storedFile.RelativePath);
                throw;
            }

            return Result.Success(ToCredentialResponse(credential), "Tải minh chứng định danh thành công.");
        }

        public async Task<Result<IReadOnlyList<CredentialResponse>>> GetIdentityDocumentsAsync(
            int accountId,
            CancellationToken cancellationToken)
        {
            var accountExists = await _unitOfWork.Repository<Account>().Query()
                .AnyAsync(account => account.AccountId == accountId, cancellationToken);
            if (!accountExists)
            {
                return Result.Failure<IReadOnlyList<CredentialResponse>>("Không tìm thấy tài khoản.", StatusCodes.Status404NotFound);
            }

            var documents = await _unitOfWork.Repository<PlayerCredential>().Query()
                .AsNoTracking()
                .Where(item => item.AccountId == accountId && item.CredentialType == IdentityDocumentType)
                .OrderByDescending(item => item.CreatedAt)
                .Select(item => new CredentialResponse(
                    item.PlayerCredentialId,
                    item.GameId,
                    item.CredentialType,
                    "Giấy tờ định danh",
                    null,
                    item.CreatedAt))
                .ToListAsync(cancellationToken);

            return Result.Success<IReadOnlyList<CredentialResponse>>(documents);
        }
}
