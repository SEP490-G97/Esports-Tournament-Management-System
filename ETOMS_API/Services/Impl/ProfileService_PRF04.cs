using ETOMS_API.DTOs.Profile;
using ETOMS_API.Models;
using ETOMS_API.Repositories.Interface;
using ETOMS_API.Services.Interface;
using ETOMS_API.Utils;
using Microsoft.EntityFrameworkCore;

namespace ETOMS_API.Services.Impl;

public sealed partial class ProfileService
{
        public async Task<Result<PublicProfileResponse>> GetPublicProfileAsync(
            int accountId,
            CancellationToken cancellationToken)
        {
            var account = await FindAccountAsync(accountId, cancellationToken);
            if (account is null)
            {
                return Result.Failure<PublicProfileResponse>("Không tìm thấy người chơi.", StatusCodes.Status404NotFound);
            }

            var gameAccounts = await _unitOfWork.Repository<PlayerCredential>().Query()
                .AsNoTracking()
                .Where(item => item.AccountId == accountId && item.CredentialType == InGameNameType)
                .Select(item => new PublicGameAccountResponse(
                    item.GameId!.Value,
                    item.Game!.Title,
                    item.Value!))
                .ToListAsync(cancellationToken);

            var teams = await _unitOfWork.Repository<TeamMember>().Query()
                .AsNoTracking()
                .Where(member => member.AccountId == accountId && member.Status == "ACTIVE")
                .Select(member => new PublicTeamResponse(
                    member.TeamId,
                    member.Team.Name,
                    member.Team.Game.Title))
                .ToListAsync(cancellationToken);

            var titles = await _unitOfWork.Repository<Registration>().Query()
                .AsNoTracking()
                .Where(registration =>
                    registration.RegistrationLineups.Any(lineup => lineup.AccountId == accountId)
                    && registration.FinalPlacement != "NONE")
                .Select(registration => new PublicTitleResponse(
                    registration.TournamentId,
                    registration.Tournament.Name,
                    registration.FinalPlacement,
                    PlacementLabel(registration.FinalPlacement)))
                .ToListAsync(cancellationToken);

            return Result.Success(new PublicProfileResponse(
                account.AccountId,
                account.FullName,
                account.AvatarUrl,
                gameAccounts,
                teams,
                titles));
        }
}
