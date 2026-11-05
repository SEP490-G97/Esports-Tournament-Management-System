using ETOMS_API.DTOs.Profile;
using ETOMS_API.HungModels;
using ETOMS_API.Repositories.Interface;
using ETOMS_API.Services.Interface;
using ETOMS_API.Utils;
using Microsoft.EntityFrameworkCore;

namespace ETOMS_API.Services.Impl;

public sealed partial class ProfileService
{
        public async Task<Result<NotificationPreferencesResponse>> GetNotificationPreferencesAsync(
            int accountId,
            CancellationToken cancellationToken)
        {
            var account = await FindAccountAsync(accountId, cancellationToken);
            return account is null
                ? Result.Failure<NotificationPreferencesResponse>("Không tìm thấy tài khoản.", StatusCodes.Status404NotFound)
                : Result.Success(new NotificationPreferencesResponse(account.NotifyInApp, account.NotifyEmail));
        }

        public async Task<Result<NotificationPreferencesResponse>> UpdateNotificationPreferencesAsync(
            int accountId,
            UpdateNotificationPreferencesRequest request,
            CancellationToken cancellationToken)
        {
            var account = await FindAccountAsync(accountId, cancellationToken);
            if (account is null)
            {
                return Result.Failure<NotificationPreferencesResponse>("Không tìm thấy tài khoản.", StatusCodes.Status404NotFound);
            }

            account.NotifyInApp = request.NotifyInApp;
            account.NotifyEmail = request.NotifyEmail;
            _unitOfWork.Repository<Account>().Update(account);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Result.Success(
                new NotificationPreferencesResponse(account.NotifyInApp, account.NotifyEmail),
                "Cập nhật cài đặt thông báo thành công.");
        }
}
