using ETOMS_API.DTOs.Profile;
using ETOMS_API.Models;
using ETOMS_API.Repositories.Interface;
using ETOMS_API.Services.Interface;
using ETOMS_API.Utils;
using Microsoft.EntityFrameworkCore;

namespace ETOMS_API.Services.Impl;

public sealed partial class ProfileService
{
        public async Task<Result<CredentialResponse>> UpsertGameAccountAsync(
            int accountId,
            int gameId,
            string inGameName,
            CancellationToken cancellationToken)
        {
            var accountExists = await _unitOfWork.Repository<Account>().Query()
                .AnyAsync(account => account.AccountId == accountId, cancellationToken);
            if (!accountExists)
            {
                return Result.Failure<CredentialResponse>("Không tìm thấy tài khoản.", StatusCodes.Status404NotFound);
            }

            var game = await _unitOfWork.Repository<Game>().Query()
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.GameId == gameId, cancellationToken);
            if (game is null)
            {
                return Result.Failure<CredentialResponse>("Không tìm thấy tựa game.", StatusCodes.Status404NotFound);
            }

            if (game.Status != "ACTIVE")
            {
                return Result<CredentialResponse>.FieldFailure(nameof(gameId), "Tựa game hiện không hoạt động.");
            }

            var normalizedName = inGameName.Trim();
            if (normalizedName.Length == 0)
            {
                return Result<CredentialResponse>.FieldFailure(nameof(inGameName), "Tên trong game không được để trống.");
            }

            var repository = _unitOfWork.Repository<PlayerCredential>();
            var credential = await repository.Query().FirstOrDefaultAsync(
                item => item.AccountId == accountId
                    && item.GameId == gameId
                    && item.CredentialType == InGameNameType,
                cancellationToken);

            if (credential is null)
            {
                credential = new PlayerCredential
                {
                    AccountId = accountId,
                    GameId = gameId,
                    CredentialType = InGameNameType,
                    Value = normalizedName
                };
                repository.Add(credential);
            }
            else
            {
                credential.Value = normalizedName;
                repository.Update(credential);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Success(ToCredentialResponse(credential), "Lưu tài khoản trong game thành công.");
        }
}
