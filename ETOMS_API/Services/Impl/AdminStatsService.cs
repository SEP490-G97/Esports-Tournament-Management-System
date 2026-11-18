using ETOMS_API.DTOs.Admin;
using ETOMS_API.Models;
using ETOMS_API.Repositories;
using ETOMS_API.Services.Interface;
using System.Globalization;
using Microsoft.EntityFrameworkCore;

namespace ETOMS_API.Services.Impl
{
    public class AdminStatsService : IAdminStatsService
    {
        private static readonly string[] TournamentStatuses = { "DRAFT", "PUBLISHED", "ONGOING", "COMPLETED", "CANCELLED" };
        private static readonly string[] PlayedStatuses = { "LIVE", "WAITING_REPORT", "DISPUTED", "VERIFIED" };
        private static readonly string[] OpenDisputeStatuses = { "OPEN", "UNDER_REVIEW" };

        private readonly EtomsDbContext _db;

        public AdminStatsService(EtomsDbContext db) => _db = db;

        public async Task<StatsResult> GetOverviewAsync(int days, int tzOffsetMinutes, CancellationToken ct = default)
        {
            if (days < 7 || days > 365) return new StatsResult(StatsError.InvalidRange);
            tzOffsetMinutes = Math.Clamp(tzOffsetMinutes, -840, 840);

            var nowUtc = DateTime.UtcNow;
            var offset = TimeSpan.FromMinutes(tzOffsetMinutes);

            // ---- Người dùng đăng ký mới theo ngày (ngày tính theo múi giờ người xem)
            var firstDay = (nowUtc + offset).Date.AddDays(-(days - 1));
            var fromUtc = firstDay - offset;

            var created = await _db.Set<Account>().AsNoTracking()
                .Where(a => a.CreatedAt >= fromUtc)
                .Select(a => a.CreatedAt)
                .ToListAsync(ct);

            var perDay = created
                .GroupBy(c => (c + offset).Date)
                .ToDictionary(g => g.Key, g => g.Count());

            var daily = new List<DailyCount>(days);
            for (int i = 0; i < days; i++)
            {
                var d = firstDay.AddDays(i);
                daily.Add(new DailyCount(d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    perDay.TryGetValue(d, out var n) ? n : 0));
            }

            var totalAccounts = await _db.Set<Account>().CountAsync(ct);

            // ---- Giải đấu
            var byStatusRows = await _db.Set<Tournament>().AsNoTracking()
                .GroupBy(t => t.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync(ct);
            var byStatus = TournamentStatuses.ToDictionary(
                s => s, s => byStatusRows.Where(r => r.Status == s).Sum(r => r.Count));

            // Top game: đếm giải đã công bố trở đi (Status <> DRAFT), cùng quy ước với vw_OrganizationReputation.
            var topRows = await _db.Set<Tournament>().AsNoTracking()
                .Where(t => t.Status != "DRAFT")
                .GroupBy(t => t.GameId)
                .Select(g => new { GameId = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count).ThenBy(x => x.GameId)
                .Take(5)
                .ToListAsync(ct);
            var topIds = topRows.Select(x => x.GameId).ToList();
            var gameInfo = await _db.Set<Game>().AsNoTracking()
                .Where(g => topIds.Contains(g.GameId))
                .Select(g => new { g.GameId, g.Title, g.ShortCode })
                .ToListAsync(ct);
            var topGames = topRows
                .Select(r =>
                {
                    var g = gameInfo.FirstOrDefault(x => x.GameId == r.GameId);
                    return new GameTournamentCount(r.GameId, g?.Title ?? $"Game #{r.GameId}", g?.ShortCode ?? "", r.Count);
                })
                .ToList();

            // ---- Trận đấu
            var matchRows = await _db.Set<Match>().AsNoTracking()
                .GroupBy(m => m.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync(ct);
            int CountOf(string s) => matchRows.Where(r => r.Status == s).Sum(r => r.Count);

            int completed = CountOf("VERIFIED") + CountOf("WALKOVER");
            int played = PlayedStatuses.Sum(s => CountOf(s));

            int disputed = await _db.Set<Match>().AsNoTracking()
                .Where(m => PlayedStatuses.Contains(m.Status) &&
                            _db.Set<Dispute>().Any(d => d.MatchId == m.MatchId))
                .CountAsync(ct);

            int open = await _db.Set<Dispute>().AsNoTracking()
                .CountAsync(d => OpenDisputeStatuses.Contains(d.Status), ct);

            decimal? rate = played == 0 ? null : Math.Round(100m * disputed / played, 1, MidpointRounding.AwayFromZero);

            var data = new StatsOverview(
                nowUtc, days, tzOffsetMinutes,
                totalAccounts, created.Count, daily,
                byStatus["ONGOING"], byStatus, topGames,
                completed, new DisputeStats(played, disputed, rate, open));

            return new StatsResult(StatsError.None, data);
        }
    }
}
