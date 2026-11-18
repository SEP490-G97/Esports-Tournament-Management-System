using ETOMS_API.DTOs.Admin;
using ETOMS_API.Models;
using ETOMS_API.Repositories;
using ETOMS_API.Services.Interface;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;
using ETOMS_API.Utils;
namespace ETOMS_API.Services.Impl
{
    public class AdminGameService : IAdminGameService
    {
        private static readonly Regex ShortCodeRx = new(@"^[A-Z0-9_]{2,10}$", RegexOptions.Compiled);

        private readonly EtomsDbContext _db;
        private readonly ILogger<AdminGameService> _log;

        public AdminGameService(EtomsDbContext db, ILogger<AdminGameService> log)
        {
            _db = db;
            _log = log;
        }

        /// <summary>
        /// Phân loại lỗi khi lưu: chỉ trả TitleTaken/CodeTaken khi DB thật sự báo trùng khóa duy nhất (MySQL 1062).
        /// Mọi lỗi khác (CHECK constraint, cột sai, …) được ghi log đầy đủ và trả SaveFailed, không đội lốt "trùng tên".
        /// </summary>
        private async Task<GameError> ClassifySaveFailureAsync(DbUpdateException ex, string title, string code, int? excludeId, CancellationToken ct)
        {
            _db.ChangeTracker.Clear();
            bool duplicate = false;
            for (Exception? e = ex; e != null; e = e.InnerException)
                if (e.Message.Contains("Duplicate entry", StringComparison.OrdinalIgnoreCase)) { duplicate = true; break; }

            if (duplicate)
            {
                var again = await FindDuplicateAsync(title, code, excludeId, ct);
                return again == GameError.None ? GameError.TitleTaken : again;
            }

            _log.LogError(ex, "Lưu game thất bại (không phải trùng khóa): {Detail}", ex.InnerException?.Message ?? ex.Message);
            return GameError.SaveFailed;
        }

        // ---------------------------------------------------------------- đọc

        public async Task<GameListResult> ListAsync(GameListQuery query, CancellationToken ct = default)
        {
            var status = (query.Status ?? "ALL").Trim().ToUpperInvariant();
            if (status.Length == 0) status = "ALL";
            if (status != "ALL" && status != AdminConstants.GameActive && status != AdminConstants.GameInactive)
                return new GameListResult(GameError.InvalidFilter);

            var q = (query.Q ?? "").Trim();
            if (q.Length > 100) q = q.Substring(0, 100);

            var baseQuery = _db.Set<Game>().AsNoTracking().AsQueryable();
            if (q.Length > 0)
                baseQuery = baseQuery.Where(g => g.Title.Contains(q) || g.ShortCode.Contains(q));

            // Số lượng cho tab: tính theo bộ lọc q nhưng KHÔNG áp bộ lọc trạng thái.
            var grouped = await baseQuery.GroupBy(g => g.Status)
                .Select(x => new { Status = x.Key, Count = x.Count() })
                .ToListAsync(ct);
            int active = grouped.Where(x => x.Status == AdminConstants.GameActive).Sum(x => x.Count);
            int inactive = grouped.Where(x => x.Status == AdminConstants.GameInactive).Sum(x => x.Count);
            var counts = new GameStatusCounts(active + inactive, active, inactive);

            var filtered = status == "ALL" ? baseQuery : baseQuery.Where(g => g.Status == status);
            var games = await filtered.OrderBy(g => g.Title).ToListAsync(ct);

            var tourCounts = await TournamentCountsAsync(ct);
            var items = games.Select(g => ToDetail(g, tourCounts.TryGetValue(g.GameId, out var c) ? c : 0)).ToList();

            return new GameListResult(GameError.None, new GameListResponse(items, counts));
        }

        public async Task<GameResult> GetAsync(int gameId, CancellationToken ct = default)
        {
            var g = await _db.Set<Game>().AsNoTracking().FirstOrDefaultAsync(x => x.GameId == gameId, ct);
            if (g == null) return new GameResult(GameError.NotFound);
            return new GameResult(GameError.None, ToDetail(g, await CountTournamentsAsync(gameId, ct)));
        }

        // ---------------------------------------------------------------- ghi

        public async Task<GameResult> CreateAsync(int actorId, SaveGameRequest request, CancellationToken ct = default)
        {
            var (error, v) = Validate(request);
            if (error != GameError.None) return new GameResult(error);

            var dup = await FindDuplicateAsync(v!.Title, v.ShortCode, null, ct);
            if (dup != GameError.None) return new GameResult(dup);

            var game = new Game
            {
                Title = v.Title,
                ShortCode = v.ShortCode,
                Status = AdminConstants.GameActive,
                MinStarters = v.MinStarters,
                MaxStarters = v.MaxStarters,
                MaxSubstitutes = v.MaxSubstitutes,
                DefaultFormat = v.DefaultFormat,
                AllowedEvidenceTypes = v.Evidence,
                MaxEvidenceFileSizeMb = v.MaxFileMb
            };

            _db.Set<Game>().Add(game);
            try
            {
                // Hai lần lưu (cần GameId để ghi audit) nằm trong một giao dịch: hoặc có cả game + log, hoặc không có gì.
                await using var tx = await _db.Database.BeginTransactionAsync(ct);
                await _db.SaveChangesAsync(ct);
                AdminAudit.Add(_db, actorId, AdminAuditActions.GameCreate, "GAME", game.GameId, null, Snapshot(game), null);
                await _db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
            }
            catch (DbUpdateException ex)
            {
                return new GameResult(await ClassifySaveFailureAsync(ex, v.Title, v.ShortCode, null, ct));
            }

            return new GameResult(GameError.None, ToDetail(game, 0));
        }

        public async Task<GameResult> UpdateAsync(int actorId, int gameId, SaveGameRequest request, CancellationToken ct = default)
        {
            var (error, v) = Validate(request);
            if (error != GameError.None) return new GameResult(error);

            var game = await _db.Set<Game>().FirstOrDefaultAsync(x => x.GameId == gameId, ct);
            if (game == null) return new GameResult(GameError.NotFound);

            var dup = await FindDuplicateAsync(v!.Title, v.ShortCode, gameId, ct);
            if (dup != GameError.None) return new GameResult(dup);

            var before = Snapshot(game);
            game.Title = v.Title;
            game.ShortCode = v.ShortCode;
            game.MinStarters = v.MinStarters;
            game.MaxStarters = v.MaxStarters;
            game.MaxSubstitutes = v.MaxSubstitutes;
            game.DefaultFormat = v.DefaultFormat;
            game.AllowedEvidenceTypes = v.Evidence;
            game.MaxEvidenceFileSizeMb = v.MaxFileMb;

            AdminAudit.Add(_db, actorId, AdminAuditActions.GameUpdate, "GAME", gameId, before, Snapshot(game), null);
            try
            {
                await _db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex)
            {
                return new GameResult(await ClassifySaveFailureAsync(ex, v.Title, v.ShortCode, gameId, ct));
            }

            return new GameResult(GameError.None, ToDetail(game, await CountTournamentsAsync(gameId, ct)));
        }

        public async Task<GameResult> SetActiveAsync(int actorId, int gameId, bool active, CancellationToken ct = default)
        {
            var game = await _db.Set<Game>().FirstOrDefaultAsync(x => x.GameId == gameId, ct);
            if (game == null) return new GameResult(GameError.NotFound);

            var target = active ? AdminConstants.GameActive : AdminConstants.GameInactive;
            if (game.Status == target) return new GameResult(GameError.AlreadyInState);

            var before = new { status = game.Status };
            game.Status = target;
            AdminAudit.Add(_db, actorId, AdminAuditActions.GameStatus,
                "GAME", gameId, before, new { status = target }, null);
            await _db.SaveChangesAsync(ct);

            return new GameResult(GameError.None, ToDetail(game, await CountTournamentsAsync(gameId, ct)));
        }

        public async Task<GameImageResult> SetImageAsync(int actorId, int gameId, GameImageKind kind, string? url, CancellationToken ct = default)
        {
            var game = await _db.Set<Game>().FirstOrDefaultAsync(x => x.GameId == gameId, ct);
            if (game == null) return new GameImageResult(GameError.NotFound);

            var old = kind == GameImageKind.Logo ? game.LogoUrl : game.BannerUrl;
            if (kind == GameImageKind.Logo) game.LogoUrl = url; else game.BannerUrl = url;

            var field = kind == GameImageKind.Logo ? "logoUrl" : "bannerUrl";
            AdminAudit.Add(_db, actorId, AdminAuditActions.GameUpdate, "GAME", gameId,
                new Dictionary<string, string?> { [field] = old },
                new Dictionary<string, string?> { [field] = url }, null);
            await _db.SaveChangesAsync(ct);

            return new GameImageResult(GameError.None, old, ToDetail(game, await CountTournamentsAsync(gameId, ct)));
        }

        public async Task<GameDeleteResult> DeleteAsync(int actorId, int gameId, CancellationToken ct = default)
        {
            var game = await _db.Set<Game>().FirstOrDefaultAsync(x => x.GameId == gameId, ct);
            if (game == null) return new GameDeleteResult(GameError.NotFound);

            // Quy tắc nghiệp vụ: không xóa cứng game đã có giải đấu (kể cả giải nháp/đã hủy).
            if (await _db.Set<Tournament>().AnyAsync(t => t.GameId == gameId, ct))
                return new GameDeleteResult(GameError.HasTournaments);

            // Các bảng khác cũng có FK tới Game: có dữ liệu thì cũng không xóa được.
            if (await _db.Set<GameRuleset>().AnyAsync(r => r.GameId == gameId, ct) ||
                await _db.Set<Team>().AnyAsync(t => t.GameId == gameId, ct) ||
                await _db.Set<TeamMember>().AnyAsync(t => t.GameId == gameId, ct) ||
                await _db.Set<RecruitmentPost>().AnyAsync(p => p.GameId == gameId, ct) ||
                await _db.Set<PlayerCredential>().AnyAsync(c => c.GameId == gameId, ct))
                return new GameDeleteResult(GameError.InUse);

            var logo = game.LogoUrl;
            var banner = game.BannerUrl;

            AdminAudit.Add(_db, actorId, AdminAuditActions.GameDelete, "GAME", gameId, Snapshot(game), null, game.Title);
            _db.Set<Game>().Remove(game);
            try
            {
                await _db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                // Có người vừa tạo dữ liệu tham chiếu giữa lúc kiểm tra và lúc xóa.
                _db.ChangeTracker.Clear();
                return new GameDeleteResult(GameError.InUse);
            }

            return new GameDeleteResult(GameError.None, logo, banner);
        }

        // ---------------------------------------------------------------- nội bộ

        private sealed record Valid(string Title, string ShortCode, int MinStarters, int MaxStarters,
            int MaxSubstitutes, string DefaultFormat, string Evidence, int MaxFileMb);

        private static (GameError, Valid?) Validate(SaveGameRequest r)
        {
            var title = (r.Title ?? "").Trim();
            if (title.Length < 2 || title.Length > 150) return (GameError.InvalidTitle, null);

            var code = (r.ShortCode ?? "").Trim().ToUpperInvariant();
            if (!ShortCodeRx.IsMatch(code)) return (GameError.InvalidShortCode, null);

            // Khớp CK_Game_Roster: MinStarters >= 1, MaxStarters >= MinStarters, MaxSubstitutes >= 0
            if (r.MinStarters < 1 || r.MaxStarters < r.MinStarters || r.MaxStarters > 50 ||
                r.MaxSubstitutes < 0 || r.MaxSubstitutes > 50)
                return (GameError.InvalidRoster, null);

            var format = (r.DefaultFormat ?? "").Trim().ToUpperInvariant();
            if (!AdminConstants.Formats.Contains(format)) return (GameError.InvalidFormat, null);

            var picked = (r.AllowedEvidenceTypes ?? Array.Empty<string>())
                .Select(x => (x ?? "").Trim().ToUpperInvariant())
                .Where(x => x.Length > 0)
                .Distinct()
                .ToList();
            if (picked.Count == 0 || picked.Any(x => !AdminConstants.EvidenceTypes.Contains(x)))
                return (GameError.InvalidEvidenceTypes, null);
            // Thứ tự chuẩn để so sánh/hiển thị ổn định.
            var evidence = string.Join(",", AdminConstants.EvidenceTypes.Where(picked.Contains));

            if (r.MaxEvidenceFileSizeMb < 1 || r.MaxEvidenceFileSizeMb > AdminConstants.MaxEvidenceFileSizeMb)
                return (GameError.InvalidFileSize, null);

            return (GameError.None, new Valid(title, code, r.MinStarters, r.MaxStarters, r.MaxSubstitutes,
                format, evidence, r.MaxEvidenceFileSizeMb));
        }

        private async Task<GameError> FindDuplicateAsync(string title, string code, int? excludeId, CancellationToken ct)
        {
            var q = _db.Set<Game>().AsNoTracking().Where(g => excludeId == null || g.GameId != excludeId);
            if (await q.AnyAsync(g => g.Title == title, ct)) return GameError.TitleTaken;
            if (await q.AnyAsync(g => g.ShortCode == code, ct)) return GameError.CodeTaken;
            return GameError.None;
        }

        private Task<int> CountTournamentsAsync(int gameId, CancellationToken ct) =>
            _db.Set<Tournament>().CountAsync(t => t.GameId == gameId, ct);

        private async Task<Dictionary<int, int>> TournamentCountsAsync(CancellationToken ct)
        {
            var rows = await _db.Set<Tournament>().AsNoTracking()
                .GroupBy(t => t.GameId)
                .Select(g => new { GameId = g.Key, Count = g.Count() })
                .ToListAsync(ct);
            return rows.ToDictionary(x => x.GameId, x => x.Count);
        }

        private static object Snapshot(Game g) => new
        {
            title = g.Title,
            shortCode = g.ShortCode,
            status = g.Status,
            minStarters = g.MinStarters,
            maxStarters = g.MaxStarters,
            maxSubstitutes = g.MaxSubstitutes,
            defaultFormat = g.DefaultFormat,
            allowedEvidenceTypes = g.AllowedEvidenceTypes,
            maxEvidenceFileSizeMb = g.MaxEvidenceFileSizeMb,
            logoUrl = g.LogoUrl,
            bannerUrl = g.BannerUrl
        };

        private static GameDetail ToDetail(Game g, int tournamentCount) => new(
            g.GameId, g.Title, g.ShortCode, g.LogoUrl, g.BannerUrl, g.Status,
            g.MinStarters, g.MaxStarters, g.MaxSubstitutes, g.DefaultFormat,
            (g.AllowedEvidenceTypes ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            g.MaxEvidenceFileSizeMb, tournamentCount);
    }
}
