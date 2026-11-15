using ETOMS_API.DTOs.Profile;
using ETOMS_API.Models;
using ETOMS_API.Repositories.Interface;
using ETOMS_API.Services.Interface;
using ETOMS_API.Utils;
using Microsoft.EntityFrameworkCore;

namespace ETOMS_API.Services.Impl;

public sealed partial class ProfileService
{
        public async Task<Result<ProfileResponse>> GetProfileAsync(int accountId, CancellationToken cancellationToken)
        {
            var account = await FindAccountAsync(accountId, cancellationToken);
            return account is null
                ? Result.Failure<ProfileResponse>("Không tìm thấy tài khoản.", StatusCodes.Status404NotFound)
                : Result.Success(ToProfileResponse(account));
        }

        public async Task<Result<ProfileResponse>> UpdateProfileAsync(
            int accountId,
            UpdateProfileRequest request,
            CancellationToken cancellationToken)
        {
            var account = await FindAccountAsync(accountId, cancellationToken);
            if (account is null)
            {
                return Result.Failure<ProfileResponse>("Không tìm thấy tài khoản.", StatusCodes.Status404NotFound);
            }

            var fullName = request.FullName.Trim();
            if (fullName.Length == 0)
            {
                return Result<ProfileResponse>.FieldFailure(nameof(request.FullName), "Họ tên không được để trống.");
            }

            account.FullName = fullName;
            account.AvatarUrl = NormalizeOptional(request.AvatarUrl);
            account.DateOfBirth = request.DateOfBirth;
            account.Phone = NormalizeOptional(request.Phone);
            account.SocialLinks = NormalizeOptional(request.SocialLinks);
            _unitOfWork.Repository<Account>().Update(account);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Result.Success(ToProfileResponse(account), "Cập nhật hồ sơ thành công.");
        }
}
