using System.Globalization;
using ETOMS_API.DTOs.Tournament;
using ETOMS_API.Repositories;
using ETOMS_API.Services.Interface;
using ETOMS_API.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using TournamentEntity = ETOMS_API.Models.Tournament;

namespace ETOMS_API.Services.Impl;

// POR-02 / UC-36 — danh sách & tìm kiếm giải công khai.
// UC-36 E2: giải Nháp không bao giờ có trong danh sách. Giải đã hủy chỉ hiện khi lọc đúng trạng thái "Đã hủy".
public sealed class TournamentDirectoryService : ITournamentDirectoryService
{
    private const int MaxPageSize = 48;
    private static readonly string[] States = { "REGISTRATION", "UPCOMING", "ONGOING", "COMPLETED", "CANCELLED" };
    private static readonly string[] Sorts = { "RELEVANT", "START", "START_DESC", "NEWEST", "NAME" };
    private static readonly string[] Fees = { "FREE", "PAID" };

    private readonly EtomsDbContext _db;

    private readonly Microsoft.Extensions.Caching.Memory.IMemoryCache _cache;

    public TournamentDirectoryService(EtomsDbContext db, Microsoft.Extensions.Caching.Memory.IMemoryCache cache)
    {
        _db = db;
        _cache = cache;
    }

    private static bool TryParseDate(string? raw, out DateTime? value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(raw)) return true;
        if (!DateTime.TryParseExact(raw.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)) return false;
        value = d;
        return true;
    }

    public async Task<Result<TournamentDirectoryPage>> SearchAsync(TournamentDirectoryQuery query, CancellationToken ct)
    {
        // ---- Kiểm tra tham số (thông báo tiếng Việt, khóa field khớp ô lọc trên trang) ----
        var q = (query.Q ?? "").Trim();
        if (q.Length > 100) return Result<TournamentDirectoryPage>.FieldFailure("Q", "Từ khóa tìm kiếm tối đa 100 ký tự.");

        var state = (query.State ?? "").Trim().ToUpperInvariant();
        if (state.Length > 0 && !States.Contains(state)) return Result<TournamentDirectoryPage>.FieldFailure("State", "Trạng thái lọc không hợp lệ.");

        var sort = string.IsNullOrWhiteSpace(query.Sort) ? "RELEVANT" : query.Sort.Trim().ToUpperInvariant();
        if (!Sorts.Contains(sort)) return Result<TournamentDirectoryPage>.FieldFailure("Sort", "Cách sắp xếp không hợp lệ.");

        var fee = (query.Fee ?? "").Trim().ToUpperInvariant();
        if (fee.Length > 0 && !Fees.Contains(fee)) return Result<TournamentDirectoryPage>.FieldFailure("Fee", "Bộ lọc lệ phí không hợp lệ.");

        if (!TryParseDate(query.From, out var from)) return Result<TournamentDirectoryPage>.FieldFailure("From", "Ngày bắt đầu lọc không hợp lệ.");
        if (!TryParseDate(query.To, out var to)) return Result<TournamentDirectoryPage>.FieldFailure("To", "Ngày kết thúc lọc không hợp lệ.");
        if (from.HasValue && to.HasValue && from > to) return Result<TournamentDirectoryPage>.FieldFailure("To", "Đến ngày phải sau hoặc bằng Từ ngày.");

        var region = (query.Region ?? "").Trim();
        if (region.Length > 100) return Result<TournamentDirectoryPage>.FieldFailure("Region", "Khu vực không hợp lệ.");
        if (query.GameId is <= 0) return Result<TournamentDirectoryPage>.FieldFailure("GameId", "Tựa game không hợp lệ.");

        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, MaxPageSize);

        // ---- Truy vấn ----
        var now = DateTime.UtcNow;
        IQueryable<TournamentEntity> src = _db.Tournaments.AsNoTracking().Where(t => t.Status != "DRAFT");

        src = state switch
        {
            "REGISTRATION" => src.Where(t => t.Status == "PUBLISHED" && t.BracketLockedAt == null && t.RegOpenAt <= now && now < t.RegCloseAt),
            "UPCOMING" => src.Where(t => t.Status == "PUBLISHED"),
            "" => src.Where(t => t.Status != "CANCELLED"),
            _ => src.Where(t => t.Status == state)
        };

        if (q.Length > 0)                                    // UC-36 A1: khớp tên giải và đơn vị tổ chức
            src = src.Where(t => t.Name.Contains(q) || (t.OrganizingUnit != null && t.OrganizingUnit.Contains(q)));
        if (query.GameId.HasValue) src = src.Where(t => t.GameId == query.GameId.Value);
        if (from.HasValue) src = src.Where(t => t.StartAt >= from.Value);
        if (to.HasValue) { var toEnd = to.Value.AddDays(1); src = src.Where(t => t.StartAt < toEnd); }
        if (region.Equals("ONLINE", StringComparison.OrdinalIgnoreCase)) src = src.Where(t => t.IsOnline);
        else if (region.Length > 0) src = src.Where(t => t.Region == region);
        if (fee == "FREE") src = src.Where(t => t.EntryFee == 0);
        else if (fee == "PAID") src = src.Where(t => t.EntryFee > 0);

        src = sort switch
        {
            "START_DESC" => src.OrderByDescending(t => t.StartAt).ThenBy(t => t.TournamentId),
            "NEWEST" => src.OrderByDescending(t => t.CreatedAt).ThenByDescending(t => t.TournamentId),
            "NAME" => src.OrderBy(t => t.Name).ThenBy(t => t.TournamentId),
            "START" => src.OrderBy(t => t.StartAt).ThenBy(t => t.TournamentId),
            // Mặc định: đang mở đăng ký -> sắp diễn ra -> đang diễn ra -> đã kết thúc -> đã hủy.
            _ => src.OrderBy(t => t.Status == "PUBLISHED" && t.BracketLockedAt == null && t.RegOpenAt <= now && now < t.RegCloseAt ? 0
                    : t.Status == "PUBLISHED" ? 1 : t.Status == "ONGOING" ? 2 : t.Status == "COMPLETED" ? 3 : 4)
                .ThenBy(t => t.StartAt).ThenBy(t => t.TournamentId)
        };

        var total = await src.CountAsync(ct);
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        page = Math.Min(page, totalPages);

        var rows = await src.Skip((page - 1) * pageSize).Take(pageSize)
            .Select(t => new
            {
                t.TournamentId, t.Name, t.GameId, Game = t.Game.Title, t.Status, t.BracketLockedAt,
                t.RegOpenAt, t.RegCloseAt, t.StartAt, t.EndAt, t.Region, t.IsOnline, t.EntryFee,
                t.PrizeStructure, t.OrganizingUnit, t.Format, t.MaxTeams, t.BannerUrl,
                Teams = t.Registrations.Count(r => r.Status == "APPROVED")
            })
            .ToListAsync(ct);

        var items = rows.Select(t =>
        {
            var open = t.Status == "PUBLISHED" && t.BracketLockedAt == null && t.RegOpenAt <= now && now < t.RegCloseAt;
            return new TournamentCardDto(
                t.TournamentId, t.Name, t.GameId, t.Game, t.Status,
                open ? "Đang mở đăng ký" : t.Status == "PUBLISHED" ? "Sắp diễn ra" : StatusText.Tournament(t.Status),
                open ? "badge-active" : StatusText.TournamentClass(t.Status),
                open, t.StartAt, t.EndAt, t.RegCloseAt, t.Region, t.IsOnline, t.EntryFee,
                string.IsNullOrWhiteSpace(t.PrizeStructure) ? null : t.PrizeStructure,
                t.OrganizingUnit, FormatLabel(t.Format), t.Teams, t.MaxTeams,
                string.IsNullOrWhiteSpace(t.BannerUrl) ? "/images/tournament-cover.svg" : t.BannerUrl!);
        }).ToList();

        return Result<TournamentDirectoryPage>.Success(new TournamentDirectoryPage(items, total, page, pageSize, totalPages));
    }

    // Ô lọc (game, khu vực) gần như không đổi — nhớ 60 giây, tiết kiệm 3 lượt database ở xa.
    public async Task<TournamentDirectoryFilters> GetFiltersAsync(CancellationToken ct)
        => (await _cache.GetOrCreateAsync("lookup:directory-filters", async e =>
        {
            e.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60);
            return await LoadFiltersAsync(ct);
        }))!;

    private async Task<TournamentDirectoryFilters> LoadFiltersAsync(CancellationToken ct)
    {
        var games = await _db.Games.AsNoTracking().Where(g => g.Status == "ACTIVE").OrderBy(g => g.Title)
            .Select(g => new DirectoryGameOption(g.GameId, g.Title)).ToListAsync(ct);
        var visible = _db.Tournaments.AsNoTracking().Where(t => t.Status != "DRAFT");
        var regions = await visible.Where(t => t.Region != null && t.Region != "")
            .Select(t => t.Region!).Distinct().OrderBy(r => r).ToListAsync(ct);
        var hasOnline = await visible.AnyAsync(t => t.IsOnline, ct);
        return new TournamentDirectoryFilters(games, regions, hasOnline);
    }

    private const string NotFoundMessage = "Không tìm thấy giải đấu hoặc giải chưa được công bố.";

    // UC-36 E2: giải Nháp không công khai — chỉ Ban tổ chức của giải được xem trước.
    private static bool Visible(string status, int organizerId, int? viewerId)
        => status != "DRAFT" || (viewerId.HasValue && viewerId.Value == organizerId);

    public async Task<Result<TournamentOverviewDto>> GetOverviewAsync(int tournamentId, int? viewerAccountId, CancellationToken ct)
    {
        var t = await _db.Tournaments.AsNoTracking().Where(x => x.TournamentId == tournamentId)
            .Select(x => new
            {
                x.TournamentId, x.Name, Game = x.Game.Title, x.Status, x.OrganizerAccountId, x.Description,
                x.OrganizingUnit, x.ContactInfo, x.IsOnline, x.Region, x.Venue, x.Format, x.GroupCount,
                x.AdvancePerGroup, x.HasThirdPlaceMatch, x.MaxTeams, x.StarterCount, x.MaxSubstitutes,
                x.RegOpenAt, x.RegCloseAt, x.StartAt, x.EndAt, x.EntryFee, x.PrizeStructure,
                x.RequireIdentityDocument, x.RequirePaymentProof, x.RequireInGameName, x.RulebookVersion,
                HasRulebook = x.RulebookContent != null && x.RulebookContent != "",
                x.BracketLockedAt, x.CancelReason, x.BannerUrl,
                Approved = x.Registrations.Count(r => r.Status == "APPROVED"),
                Waitlisted = x.Registrations.Count(r => r.Status == "WAITLISTED"),
                LiveStreams = x.Livestreams.Count(l => l.Status == "LIVE")
            })
            .FirstOrDefaultAsync(ct);
        if (t is null || !Visible(t.Status, t.OrganizerAccountId, viewerAccountId))
            return Result.Failure<TournamentOverviewDto>(NotFoundMessage, StatusCodes.Status404NotFound);

        var now = DateTime.UtcNow;
        var open = t.Status == "PUBLISHED" && t.BracketLockedAt == null && t.RegOpenAt <= now && now < t.RegCloseAt;
        return Result<TournamentOverviewDto>.Success(new TournamentOverviewDto(
            t.TournamentId, t.Name, t.Game, t.Status,
            open ? "Đang mở đăng ký" : t.Status == "PUBLISHED" ? "Sắp diễn ra" : StatusText.Tournament(t.Status),
            open ? "badge-active" : StatusText.TournamentClass(t.Status),
            open, viewerAccountId == t.OrganizerAccountId,
            t.Description, t.OrganizingUnit, t.ContactInfo, t.IsOnline, t.Region, t.Venue, FormatLabel(t.Format),
            t.GroupCount, t.AdvancePerGroup, t.HasThirdPlaceMatch, t.MaxTeams, t.StarterCount, t.MaxSubstitutes,
            t.RegOpenAt, t.RegCloseAt, t.StartAt, t.EndAt, t.EntryFee,
            string.IsNullOrWhiteSpace(t.PrizeStructure) ? null : t.PrizeStructure,
            t.RequireIdentityDocument, t.RequirePaymentProof, t.RequireInGameName, t.RulebookVersion, t.HasRulebook,
            t.BracketLockedAt != null, t.Status == "CANCELLED" ? t.CancelReason : null,
            t.Approved, t.Waitlisted,
            string.IsNullOrWhiteSpace(t.BannerUrl) ? "/images/tournament-cover.svg" : t.BannerUrl!,
            t.LiveStreams));
    }

    public async Task<Result<TournamentParticipantsDto>> GetParticipantsAsync(int tournamentId, int? viewerAccountId, CancellationToken ct)
    {
        var t = await _db.Tournaments.AsNoTracking().Where(x => x.TournamentId == tournamentId)
            .Select(x => new { x.Name, x.Status, x.OrganizerAccountId, x.MaxTeams })
            .FirstOrDefaultAsync(ct);
        if (t is null || !Visible(t.Status, t.OrganizerAccountId, viewerAccountId))
            return Result.Failure<TournamentParticipantsDto>(NotFoundMessage, StatusCodes.Status404NotFound);

        var rows = await _db.Registrations.AsNoTracking()
            .Where(r => r.TournamentId == tournamentId && (r.Status == "APPROVED" || r.Status == "WAITLISTED"))
            .Select(r => new
            {
                r.Status, r.TeamId, r.Team.Name, r.Team.Tag, r.Team.LogoUrl, r.WaitlistPosition,
                r.FinalRank, r.FinalPlacement, r.ReviewedAt
            })
            .ToListAsync(ct);

        static string? Placement(string p) => p switch
        {
            "CHAMPION" => "Vô địch", "RUNNER_UP" => "Á quân", "THIRD" => "Hạng ba", _ => null
        };

        var approved = rows.Where(r => r.Status == "APPROVED")
            // Giải đã có thứ hạng thì xếp theo hạng; chưa có thì theo thứ tự được duyệt.
            .OrderBy(r => r.FinalRank ?? int.MaxValue).ThenBy(r => r.ReviewedAt).ThenBy(r => r.Name)
            .Select(r => new ParticipantDto(r.TeamId, r.Name, r.Tag, r.LogoUrl, null, r.FinalRank, Placement(r.FinalPlacement)))
            .ToList();
        var waitlisted = rows.Where(r => r.Status == "WAITLISTED")
            .OrderBy(r => r.WaitlistPosition ?? int.MaxValue).ThenBy(r => r.Name)
            .Select(r => new ParticipantDto(r.TeamId, r.Name, r.Tag, r.LogoUrl, r.WaitlistPosition, null, null))
            .ToList();

        return Result<TournamentParticipantsDto>.Success(new TournamentParticipantsDto(tournamentId, t.Name, t.MaxTeams, approved, waitlisted));
    }

    public static string FormatLabel(string format) => format switch
    {
        "SINGLE_ELIMINATION" => "Loại trực tiếp",
        "DOUBLE_ELIMINATION" => "Loại kép",
        "ROUND_ROBIN" => "Vòng tròn",
        "HYBRID" => "Vòng bảng rồi loại trực tiếp",
        _ => format
    };
}
