using ETOMS_API.DTOs.Profile;
using ETOMS_API.Models;
using ETOMS_API.Repositories.Interface;
using ETOMS_API.Services.Interface;
using ETOMS_API.Utils;
using Microsoft.EntityFrameworkCore;

namespace ETOMS_API.Services.Impl;

public sealed partial class ProfileService
{
        public async Task<Result<IReadOnlyList<MatchHistoryResponse>>> GetMatchHistoryAsync(
            int accountId,
            CancellationToken cancellationToken)
        {
            var accountExists = await _unitOfWork.Repository<Account>().Query()
                .AnyAsync(account => account.AccountID == accountId, cancellationToken);
            if (!accountExists)
            {
                return Result.Failure<IReadOnlyList<MatchHistoryResponse>>("Không tìm thấy tài khoản.", StatusCodes.Status404NotFound);
            }

            var lineups = await _unitOfWork.Repository<RegistrationLineup>().Query()
                .AsNoTracking()
                .Where(lineup => lineup.AccountID == accountId)
                .Select(lineup => new
                {
                    lineup.Registration.TournamentID,
                    lineup.Registration.TeamID,
                    lineup.Registration.Tournament.Name,
                    TeamName = lineup.Registration.Team.Name,
                    lineup.Slot
                })
                .ToListAsync(cancellationToken);

            if (lineups.Count == 0)
            {
                return Result.Success<IReadOnlyList<MatchHistoryResponse>>([]);
            }

            var teamByTournament = lineups
                .GroupBy(item => item.TournamentID)
                .ToDictionary(group => group.Key, group => group.First());
            var tournamentIds = teamByTournament.Keys.ToArray();
            var teamIds = teamByTournament.Values.Select(item => item.TeamID).Distinct().ToArray();

            var matches = await _unitOfWork.Repository<Match>().Query()
                .AsNoTracking()
                .Where(match => tournamentIds.Contains(match.TournamentID)
                    && ((match.TeamAID.HasValue && teamIds.Contains(match.TeamAID.Value))
                        || (match.TeamBID.HasValue && teamIds.Contains(match.TeamBID.Value))))
                .Select(match => new
                {
                    match.MatchID,
                    match.TournamentID,
                    match.TeamAID,
                    match.TeamBID,
                    match.ScoreA,
                    match.ScoreB,
                    match.ScheduledAt,
                    match.Status,
                    TournamentName = match.Tournament.Name,
                    TeamAName = match.TeamA == null ? null : match.TeamA.Name,
                    TeamBName = match.TeamB == null ? null : match.TeamB.Name
                })
                .ToListAsync(cancellationToken);

            var history = new List<MatchHistoryResponse>();
            foreach (var match in matches)
            {
                if (!teamByTournament.TryGetValue(match.TournamentID, out var participant))
                {
                    continue;
                }

                var isTeamA = match.TeamAID == participant.TeamID;
                var isTeamB = match.TeamBID == participant.TeamID;
                if (!isTeamA && !isTeamB)
                {
                    continue;
                }

                history.Add(new MatchHistoryResponse(
                    match.MatchID,
                    match.TournamentName,
                    isTeamA ? match.TeamAName! : match.TeamBName!,
                    participant.Slot,
                    match.ScheduledAt,
                    isTeamA ? match.ScoreA : match.ScoreB,
                    isTeamA ? match.ScoreB : match.ScoreA,
                    match.Status,
                    MatchStatusLabel(match.Status)));
            }

            return Result.Success<IReadOnlyList<MatchHistoryResponse>>(
                history.OrderByDescending(item => item.ScheduledAt).ToArray());
        }
}
