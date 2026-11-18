namespace ETOMS_API.DTOs.Admin
{
    /// <summary>Một điểm của chuỗi thời gian; Date dạng yyyy-MM-dd theo múi giờ người xem.</summary>
    public record DailyCount(string Date, int Count);

    public record GameTournamentCount(int GameId, string Title, string ShortCode, int Count);

    /// <summary>
    /// PlayedMatches: trận đã diễn ra (LIVE, WAITING_REPORT, DISPUTED, VERIFIED).
    /// DisputedMatches: số trận trong nhóm đó từng có ít nhất một tranh chấp.
    /// </summary>
    public record DisputeStats(int PlayedMatches, int DisputedMatches, decimal? RatePercent, int OpenDisputes);

    public record StatsOverview(
        DateTime GeneratedAt,
        int Days,
        int TzOffsetMinutes,
        int TotalAccounts,
        int NewUsersInRange,
        List<DailyCount> NewUsersDaily,
        int OngoingTournaments,
        Dictionary<string, int> TournamentsByStatus,
        List<GameTournamentCount> TopGames,
        int CompletedMatches,
        DisputeStats Disputes);
}
