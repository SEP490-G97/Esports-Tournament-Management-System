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
                .AnyAsync(account => account.AccountId == accountId, cancellationToken);
            if (!accountExists)
            {
                return Result.Failure<IReadOnlyList<MatchHistoryResponse>>("Không tìm thấy tài khoản.", StatusCodes.Status404NotFound);
            }

            var lineups = await _unitOfWork.Repository<RegistrationLineup>().Query()
                .AsNoTracking()
                .Where(lineup => lineup.AccountId == accountId)
                .Select(lineup => new
                {
                    lineup.Registration.TournamentId,
                    lineup.Registration.TeamId,
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
                .GroupBy(item => item.TournamentId)
                .ToDictionary(group => group.Key, group => group.First());
            var tournamentIds = teamByTournament.Keys.ToArray();
            var teamIds = teamByTournament.Values.Select(item => item.TeamId).Distinct().ToArray();

            var matches = await _unitOfWork.Repository<Match>().Query()
                .AsNoTracking()
                .Where(match => tournamentIds.Contains(match.TournamentId)
                    && ((match.TeamAid.HasValue && teamIds.Contains(match.TeamAid.Value))
                        || (match.TeamBid.HasValue && teamIds.Contains(match.TeamBid.Value))))
                .Select(match => new
                {
                    match.MatchId,
                    match.TournamentId,
                    match.TeamAid,
                    match.TeamBid,
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
                if (!teamByTournament.TryGetValue(match.TournamentId, out var participant))
                {
                    continue;
                }

                var isTeamA = match.TeamAid == participant.TeamId;
                var isTeamB = match.TeamBid == participant.TeamId;
                if (!isTeamA && !isTeamB)
                {
                    continue;
                }

                history.Add(new MatchHistoryResponse(
                    match.MatchId,
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
