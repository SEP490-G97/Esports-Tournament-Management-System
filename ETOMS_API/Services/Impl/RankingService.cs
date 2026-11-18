using ETOMS_API.DTOs.Ranking;
using ETOMS_API.Models;
using ETOMS_API.Repositories.Interface;
using ETOMS_API.Services.Interface;
using ETOMS_API.Utils;
using Microsoft.EntityFrameworkCore;

namespace ETOMS_API.Services.Impl;

public sealed class RankingService : IRankingService
{
    private readonly IUnitOfWork _unitOfWork;

    public RankingService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    private IQueryable<TEntity> Query<TEntity>() where TEntity : class
        => _unitOfWork.Repository<TEntity>().Query();

    public async Task<IReadOnlyList<LeaderboardEntryResponse>> GetLeaderboardAsync(
        int? gameId,
        int? season,
        CancellationToken cancellationToken)
    {
        var query = from ranking in Query<VwLeaderboard>().AsNoTracking()
                    join team in Query<Team>().AsNoTracking()
                        on ranking.TeamId equals team.TeamId
                    where (!gameId.HasValue || ranking.GameId == gameId.Value)
                        && (!season.HasValue || ranking.Season == season.Value)
                    select new
                    {
                        ranking.TeamId,
                        team.Name,
                        team.Tag,
                        team.LogoUrl,
                        ranking.GameId,
                        GameName = team.Game.Title,
                        Season = ranking.Season ?? 0,
                        TournamentPlayed = ranking.TournamentPlayed,
                        TitleCount = ranking.TitleCount ?? 0,
                        AchievementPoint = ranking.AchievementPoint ?? 0
                    };

        var source = await query
            .OrderByDescending(item => item.AchievementPoint)
            .ThenByDescending(item => item.TitleCount)
            .ThenByDescending(item => item.TournamentPlayed)
            .ThenBy(item => item.Name)
            .ThenBy(item => item.TeamId)
            .ToListAsync(cancellationToken);

        var rows = season.HasValue
            ? source.Select(item => new LeaderboardRow(
                item.TeamId, item.Name, item.Tag, item.LogoUrl, item.GameId, item.GameName,
                item.Season, item.TournamentPlayed, item.TitleCount, item.AchievementPoint)).ToList()
            : source.GroupBy(item => new { item.TeamId, item.GameId })
                .Select(group =>
                {
                    var first = group.First();
                    return new LeaderboardRow(
                        first.TeamId,
                        first.Name,
                        first.Tag,
                        first.LogoUrl,
                        first.GameId,
                        first.GameName,
                        null,
                        group.Sum(item => item.TournamentPlayed),
                        group.Sum(item => item.TitleCount),
                        group.Sum(item => item.AchievementPoint));
                })
                .OrderByDescending(item => item.AchievementPoint)
                .ThenByDescending(item => item.TitleCount)
                .ThenByDescending(item => item.TournamentPlayed)
                .ThenBy(item => item.Name)
                .ThenBy(item => item.TeamId)
                .ToList();

        var entries = new List<LeaderboardEntryResponse>(rows.Count);
        var rank = 0;
        (decimal Points, decimal Titles, long Tournaments)? previous = null;
        for (var index = 0; index < rows.Count; index++)
        {
            var row = rows[index];
            var score = (row.AchievementPoint, row.TitleCount, row.TournamentPlayed);
            if (previous != score)
            {
                rank = index + 1;
                previous = score;
            }

            entries.Add(new LeaderboardEntryResponse(
                rank,
                row.TeamId,
                row.Name,
                row.Tag,
                row.LogoUrl,
                row.GameId,
                row.GameName,
                row.Season,
                row.TournamentPlayed,
                row.TitleCount,
                row.AchievementPoint));
        }

        return entries;
    }

    public async Task<IReadOnlyList<int>> GetSeasonsAsync(int? gameId, CancellationToken cancellationToken)
    {
        var query = Query<VwLeaderboard>().AsNoTracking()
            .Where(item => item.Season.HasValue);
        if (gameId.HasValue)
        {
            query = query.Where(item => item.GameId == gameId.Value);
        }

        var seasons = await query.Select(item => item.Season!.Value)
            .Distinct()
            .OrderByDescending(item => item)
            .ToListAsync(cancellationToken);
        return seasons.Select(item => (int)item).ToArray();
    }

    public async Task<Result<RankedTeamProfileResponse>> GetTeamProfileAsync(
        int teamId,
        CancellationToken cancellationToken)
    {
        var team = await Query<Team>().AsNoTracking()
            .Where(item => item.TeamId == teamId && item.Status == "ACTIVE")
            .Select(item => new
            {
                item.TeamId,
                item.Name,
                item.Tag,
                item.LogoUrl,
                GameName = item.Game.Title,
                item.CreatedAt
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (team is null)
        {
            return Result<RankedTeamProfileResponse>.Failure(
                "Không tìm thấy hồ sơ đội công khai.",
                StatusCodes.Status404NotFound);
        }

        var honors = await Query<Registration>().AsNoTracking()
            .Where(item => item.TeamId == teamId
                && item.Status == "APPROVED"
                && item.Tournament.Status == "COMPLETED"
                && item.FinalRank.HasValue
                && item.FinalRank.Value > 0)
            .OrderByDescending(item => item.Tournament.StartAt)
            .Select(item => new TeamHonorResponse(
                item.TournamentId,
                item.Tournament.Name,
                item.Tournament.StartAt.Year,
                item.FinalRank!.Value,
                item.FinalPlacement == "NONE"
                    ? item.FinalRank == 1 ? "CHAMPION"
                        : item.FinalRank == 2 ? "RUNNER_UP"
                            : item.FinalRank == 3 ? "THIRD" : "PARTICIPANT"
                    : item.FinalPlacement))
            .ToListAsync(cancellationToken);

        return Result.Success(new RankedTeamProfileResponse(
            team.TeamId,
            team.Name,
            team.Tag,
            team.LogoUrl,
            team.GameName,
            AsUtcOffset(team.CreatedAt),
            honors));
    }

    public async Task<Result<TournamentReportResponse>> GetTournamentReportAsync(
        int tournamentId,
        int accountId,
        CancellationToken cancellationToken)
    {
        var tournament = await Query<Tournament>().AsNoTracking()
            .Where(item => item.TournamentId == tournamentId)
            .Select(item => new
            {
                item.TournamentId,
                item.Name,
                GameName = item.Game.Title,
                item.Status,
                item.OrganizerAccountId
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (tournament is null)
        {
            return Result<TournamentReportResponse>.Failure(
                "Không tìm thấy giải đấu.",
                StatusCodes.Status404NotFound);
        }
        if (tournament.OrganizerAccountId != accountId)
        {
            return Result<TournamentReportResponse>.Failure(
                "Chỉ ban tổ chức giải mới được xem bộ hồ sơ tổng kết.",
                StatusCodes.Status403Forbidden);
        }

        var registrations = await Query<Registration>().AsNoTracking()
            .Where(item => item.TournamentId == tournamentId && item.Status != "DRAFT")
            .OrderBy(item => item.Team.Name)
            .Select(item => new TournamentReportRegistrationResponse(
                item.RegistrationId,
                item.Team.Name,
                item.Team.Tag,
                item.Status,
                item.PaymentStatus,
                item.SubmittedAt.HasValue ? AsUtcOffset(item.SubmittedAt.Value) : null,
                item.FinalPlacement,
                item.FinalRank))
            .ToListAsync(cancellationToken);

        var payments = await Query<PaymentRecord>().AsNoTracking()
            .Where(item => item.Registration.TournamentId == tournamentId
                && item.Registration.Status != "DRAFT")
            .OrderBy(item => item.RecordedAt)
            .Select(item => new TournamentReportPaymentResponse(
                item.RegistrationId,
                item.Registration.Team.Name,
                item.RecordKind,
                item.DeclaredAmount,
                item.Note,
                item.IsCurrent,
                AsUtcOffset(item.RecordedAt)))
            .ToListAsync(cancellationToken);

        var matches = await Query<Match>().AsNoTracking()
            .Where(item => item.TournamentId == tournamentId)
            .OrderBy(item => item.ScheduledAt)
            .ThenBy(item => item.MatchId)
            .Select(item => new TournamentReportMatchResponse(
                item.MatchId,
                item.TeamA == null ? null : item.TeamA.Name,
                item.TeamB == null ? null : item.TeamB.Name,
                item.WinnerTeam == null ? null : item.WinnerTeam.Name,
                item.ScoreA,
                item.ScoreB,
                item.Status,
                item.ScheduledAt.HasValue ? AsUtcOffset(item.ScheduledAt.Value) : null,
                item.VerifiedAt.HasValue ? AsUtcOffset(item.VerifiedAt.Value) : null))
            .ToListAsync(cancellationToken);

        var disputes = await Query<Dispute>().AsNoTracking()
            .Where(item => item.Match.TournamentId == tournamentId)
            .OrderBy(item => item.OpenedAt)
            .Select(item => new TournamentReportDisputeResponse(
                item.MatchId,
                item.OpenReason,
                item.Status,
                item.RulingReason,
                item.AppealStatus,
                AsUtcOffset(item.OpenedAt),
                item.ResolvedAt.HasValue ? AsUtcOffset(item.ResolvedAt.Value) : null))
            .ToListAsync(cancellationToken);

        var standings = await (
            from standing in Query<VwGroupStanding>().AsNoTracking()
            join team in Query<Team>().AsNoTracking() on standing.TeamId equals team.TeamId
            where standing.TournamentId == tournamentId && standing.TeamId.HasValue
            orderby standing.GroupLabel, standing.Point descending, standing.ScoreDiff descending, team.Name
            select new TournamentReportStandingResponse(
                standing.GroupLabel,
                standing.TeamId!.Value,
                team.Name,
                standing.Played,
                standing.Won ?? 0m,
                standing.Lost ?? 0m,
                standing.ScoreDiff ?? 0m,
                standing.Point ?? 0m))
            .ToListAsync(cancellationToken);

        return Result.Success(new TournamentReportResponse(
            tournament.TournamentId,
            tournament.Name,
            tournament.GameName,
            tournament.Status,
            DateTimeOffset.UtcNow,
            registrations,
            payments,
            matches,
            disputes,
            standings));
    }

    public async Task FinalizeCompletedTournamentsAsync(CancellationToken cancellationToken)
    {
        var tournamentIds = await Query<Tournament>().AsNoTracking()
            .Where(item => item.Status == "COMPLETED")
            .Select(item => item.TournamentId)
            .ToListAsync(cancellationToken);

        foreach (var tournamentId in tournamentIds)
        {
            var registrations = await Query<Registration>()
                .Where(item => item.TournamentId == tournamentId && item.Status == "APPROVED")
                .ToListAsync(cancellationToken);
            if (registrations.Count == 0
                || registrations.Any(item => !item.FinalRank.HasValue || item.FinalRank.Value < 1)
                || registrations.Count(item => item.FinalRank == 1) != 1
                || registrations.Count(item => item.FinalRank == 2) != 1)
            {
                continue;
            }

            foreach (var registration in registrations.Where(item => item.FinalPlacement == "NONE"))
            {
                registration.FinalPlacement = registration.FinalRank switch
                {
                    1 => "CHAMPION",
                    2 => "RUNNER_UP",
                    3 => "THIRD",
                    _ => "PARTICIPANT"
                };
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static DateTimeOffset AsUtcOffset(DateTime value)
        => new(DateTime.SpecifyKind(value, DateTimeKind.Utc));

    private sealed record LeaderboardRow(
        int TeamId,
        string Name,
        string Tag,
        string? LogoUrl,
        int GameId,
        string GameName,
        int? Season,
        long TournamentPlayed,
        decimal TitleCount,
        decimal AchievementPoint);
}
