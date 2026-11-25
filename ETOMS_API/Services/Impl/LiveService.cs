using System.Security.Cryptography;
using System.Text;
using ETOMS_API.DTOs.Live;
using ETOMS_API.Models;
using ETOMS_API.Repositories.Interface;
using ETOMS_API.Services.Interface;
using ETOMS_API.Utils;
using Microsoft.EntityFrameworkCore;

namespace ETOMS_API.Services.Impl;

// UC-45 (LIVE-01/02/04) — Trọng tài của trận hoặc Ban tổ chức nhập tỉ số từng ván ngay khi ván kết thúc.
// Tỉ số trực tiếp chỉ để người xem theo dõi: KHÔNG ghi Match.ScoreA/ScoreB/WinnerTeamID. Kết quả chính thức
// vẫn chốt qua hai báo cáo mù của đội trưởng (RES-01/02) hoặc phán quyết tranh chấp (LI-01).
// UC-46 — Ban tổ chức thêm/sửa/xóa link livestream của giải và gán vào trận; người xem xem trình phát nhúng.
public class LiveService : ILiveService
{
    private static readonly string[] ScoringStatuses = { "READY", "LIVE", "WAITING_REPORT" };
    private static readonly string[] StreamStatuses = { "SCHEDULED", "LIVE", "ENDED" };
    private const int MaxGameScore = 999;
    private const int MaxTitleLength = 200;

    private readonly IUnitOfWork _uow;

    public LiveService(IUnitOfWork uow) => _uow = uow;

    // ===================== UC-45 tỉ số trực tiếp =====================

    public async Task<Result<MatchLiveDto>> GetMatchLiveAsync(int matchId, int? viewerAccountId)
    {
        // Trang xem tự làm mới mỗi 10–15 giây: lấy trận + các ván + livestream trong MỘT câu SQL
        // (database ở xa ~200 ms/lượt, trước đây 3 câu).
        var match = await _uow.Repository<Match>().Query().AsNoTracking()
            .Include(m => m.Tournament).Include(m => m.TeamA).Include(m => m.TeamB)
            .Include(m => m.MatchGames).Include(m => m.Livestreams)
            .FirstOrDefaultAsync(m => m.MatchId == matchId);
        if (match is null || !CanView(match.Tournament, viewerAccountId))
            return Result<MatchLiveDto>.Failure("Không tìm thấy trận đấu.", StatusCodes.Status404NotFound);
        return Result<MatchLiveDto>.Success(Build(match, match.MatchGames.OrderBy(g => g.GameNo).ToList(), match.Livestreams.ToList(), viewerAccountId));
    }

    public async Task<Result<MatchLiveDto>> SaveGameAsync(int matchId, int gameNo, int actorAccountId, SaveLiveGameRequest request)
    {
        var (match, error) = await LoadForScoringAsync(matchId, actorAccountId);
        if (error is not null) return error;
        var m = match!;

        if (gameNo < 1 || gameNo > m.BestOf)
            return Result<MatchLiveDto>.FieldFailure("GameNo", $"Ván {gameNo} không hợp lệ: trận BO{m.BestOf} chỉ có ván 1 đến ván {m.BestOf}.");

        string teamA = m.TeamA!.Name, teamB = m.TeamB!.Name;
        if (request.ScoreA is null) return Result<MatchLiveDto>.FieldFailure("ScoreA", $"Vui lòng nhập tỉ số của {teamA}.");
        if (request.ScoreB is null) return Result<MatchLiveDto>.FieldFailure("ScoreB", $"Vui lòng nhập tỉ số của {teamB}.");
        if (request.ScoreA is < 0 or > MaxGameScore) return Result<MatchLiveDto>.FieldFailure("ScoreA", $"Tỉ số mỗi ván phải từ 0 đến {MaxGameScore}.");
        if (request.ScoreB is < 0 or > MaxGameScore) return Result<MatchLiveDto>.FieldFailure("ScoreB", $"Tỉ số mỗi ván phải từ 0 đến {MaxGameScore}.");
        if (request.ScoreA == request.ScoreB)
            return Result<MatchLiveDto>.FieldFailure("ScoreB", "Mỗi ván phải có đội thắng: hai tỉ số không được bằng nhau.");

        var games = await GamesOfAsync(matchId);
        var lastNo = games.Count == 0 ? 0 : games.Max(g => g.GameNo);
        if (gameNo > lastNo + 1)
            return Result<MatchLiveDto>.FieldFailure("GameNo", $"Hãy nhập ván {lastNo + 1} trước khi nhập ván {gameNo}.");

        // Mô phỏng danh sách sau khi lưu để chặn ván thừa: loạt BO đã phân định thì không còn ván sau.
        var simulated = games.Where(g => g.GameNo != gameNo)
            .Select(g => (GameNo: g.GameNo, A: g.ScoreA ?? 0, B: g.ScoreB ?? 0))
            .Append((GameNo: gameNo, A: request.ScoreA.Value, B: request.ScoreB.Value))
            .OrderBy(g => g.GameNo).ToList();
        var decidedAt = DecidedAt(simulated, m.BestOf);
        if (decidedAt is int k && simulated.Any(g => g.GameNo > k))
        {
            var winsA = simulated.Where(g => g.GameNo <= k).Count(g => g.A > g.B);
            var leader = winsA * 2 > m.BestOf ? teamA : teamB;
            return Result<MatchLiveDto>.FieldFailure("GameNo", gameNo > k
                ? $"Loạt BO{m.BestOf} đã phân định sau ván {k} ({leader} thắng đủ {m.BestOf / 2 + 1} ván), không có ván {gameNo}."
                : $"Với tỉ số này, {leader} thắng đủ {m.BestOf / 2 + 1} ván ngay sau ván {k}, nên các ván sau không còn hợp lệ. Hãy xóa các ván sau trước.");
        }

        var now = DateTime.UtcNow;
        var game = games.FirstOrDefault(g => g.GameNo == gameNo);
        if (game is null)
        {
            game = new MatchGame { MatchId = matchId, GameNo = gameNo };
            await _uow.Repository<MatchGame>().AddAsync(game);
        }
        else _uow.Repository<MatchGame>().Update(game);
        game.ScoreA = request.ScoreA;
        game.ScoreB = request.ScoreB;
        game.WinnerTeamId = request.ScoreA > request.ScoreB ? m.TeamAid : m.TeamBid;
        game.UpdatedByAccountId = actorAccountId;
        game.UpdatedAt = now;

        // Ván đầu tiên có tỉ số nghĩa là trận đã bắt đầu.
        if (m.Status == "READY")
        {
            m.Status = "LIVE";
            _uow.Repository<Match>().Update(m);
        }

        try { await _uow.SaveChangesAsync(); }
        catch (DbUpdateException)
        {
            return Result<MatchLiveDto>.Failure("Tỉ số vừa được người khác cập nhật. Vui lòng tải lại trang rồi thử lại.", StatusCodes.Status409Conflict);
        }
        return Result<MatchLiveDto>.Success(await BuildAsync(m, actorAccountId), $"Đã lưu tỉ số ván {gameNo}.");
    }

    public async Task<Result<MatchLiveDto>> ClearGameAsync(int matchId, int gameNo, int actorAccountId)
    {
        var (match, error) = await LoadForScoringAsync(matchId, actorAccountId);
        if (error is not null) return error;

        var games = await GamesOfAsync(matchId);
        var game = games.FirstOrDefault(g => g.GameNo == gameNo);
        if (game is null) return Result<MatchLiveDto>.FieldFailure("GameNo", $"Ván {gameNo} chưa có tỉ số.");
        var lastNo = games.Max(g => g.GameNo);
        if (gameNo != lastNo)
            return Result<MatchLiveDto>.FieldFailure("GameNo", $"Chỉ xóa được ván cuối cùng (ván {lastNo}) để thứ tự các ván không bị đứt.");

        _uow.Repository<MatchGame>().Remove(game);
        await _uow.SaveChangesAsync();
        return Result<MatchLiveDto>.Success(await BuildAsync(match!, actorAccountId), $"Đã xóa tỉ số ván {gameNo}.");
    }

    private async Task<(Match? Match, Result<MatchLiveDto>? Error)> LoadForScoringAsync(int matchId, int actorAccountId)
    {
        var match = await LoadMatchAsync(matchId);
        if (match is null || !CanView(match.Tournament, actorAccountId))
            return (null, Result<MatchLiveDto>.Failure("Không tìm thấy trận đấu.", StatusCodes.Status404NotFound));
        if (!CanScore(match, actorAccountId))
            return (null, Result<MatchLiveDto>.Failure("Chỉ trọng tài của trận hoặc Ban tổ chức giải mới được nhập tỉ số trực tiếp.", StatusCodes.Status403Forbidden));
        var closed = ScoringClosedReason(match);
        if (closed is not null) return (null, Result<MatchLiveDto>.Failure(closed));
        return (match, null);
    }

    private static bool CanScore(Match m, int? accountId)
        => accountId.HasValue && (m.RefereeAccountId == accountId || m.Tournament.OrganizerAccountId == accountId);

    private static string? ScoringClosedReason(Match m)
    {
        if (m.Tournament.Status == "CANCELLED") return "Giải đã hủy, tỉ số trực tiếp đã khóa.";
        if (m.TeamAid is null || m.TeamBid is null) return "Trận chưa đủ hai đội, chưa nhập tỉ số được.";
        return m.Status switch
        {
            "PENDING" => "Trận chưa bắt đầu: hai đội cần điểm danh (hoặc Ban tổ chức điểm danh thủ công) trước khi nhập tỉ số.",
            "VERIFIED" or "WALKOVER" => "Trận đã có kết quả chính thức, tỉ số trực tiếp đã khóa.",
            "DISPUTED" => "Trận đang tranh chấp, tỉ số trực tiếp đã khóa để Trọng tài phân xử.",
            "CANCELLED" => "Trận đã hủy.",
            _ when ScoringStatuses.Contains(m.Status) => null,
            _ => "Trận không ở trạng thái nhập tỉ số."
        };
    }

    // Ván mà một đội đạt đủ số ván thắng của loạt BO; null nếu chưa phân định.
    private static int? DecidedAt(IEnumerable<(int GameNo, int A, int B)> ordered, int bestOf)
    {
        int need = bestOf / 2 + 1, a = 0, b = 0;
        foreach (var g in ordered)
        {
            if (g.A > g.B) a++; else if (g.B > g.A) b++;
            if (a >= need || b >= need) return g.GameNo;
        }
        return null;
    }

    private async Task<Match?> LoadMatchAsync(int matchId) => await _uow.Repository<Match>().Query()
        .Include(m => m.Tournament).Include(m => m.TeamA).Include(m => m.TeamB)
        .FirstOrDefaultAsync(m => m.MatchId == matchId);

    private async Task<List<MatchGame>> GamesOfAsync(int matchId) => await _uow.Repository<MatchGame>().Query()
        .Where(g => g.MatchId == matchId).OrderBy(g => g.GameNo).ToListAsync();

    private async Task<MatchLiveDto> BuildAsync(Match m, int? viewerAccountId)
    {
        var games = await _uow.Repository<MatchGame>().Query().AsNoTracking()
            .Where(g => g.MatchId == m.MatchId).OrderBy(g => g.GameNo).ToListAsync();
        var streams = await _uow.Repository<Livestream>().Query().AsNoTracking()
            .Where(l => l.MatchId == m.MatchId).ToListAsync();
        return Build(m, games, streams, viewerAccountId);
    }

    private static MatchLiveDto Build(Match m, List<MatchGame> games, List<Livestream> streams, int? viewerAccountId)
    {
        int winsA = games.Count(g => g.ScoreA > g.ScoreB), winsB = games.Count(g => g.ScoreB > g.ScoreA);
        int need = m.BestOf / 2 + 1;
        bool decided = winsA >= need || winsB >= need;
        int? leader = winsA >= need ? m.TeamAid : winsB >= need ? m.TeamBid : null;
        // Trận đã chốt (báo cáo khớp / phán quyết / thắng trắng): ô tỉ số hiện kết quả CHÍNH THỨC, không để 0 – 0 cạnh nhãn "Đã xác nhận".
        if ((m.Status == "VERIFIED" || m.Status == "WALKOVER") && m.ScoreA.HasValue && m.ScoreB.HasValue)
        {
            winsA = m.ScoreA.Value; winsB = m.ScoreB.Value;
            decided = true; leader = m.WinnerTeamId ?? leader;
        }
        var closed = ScoringClosedReason(m);
        int lastNo = games.Count == 0 ? 0 : games.Max(g => g.GameNo);
        int? next = closed is null && !decided && lastNo < m.BestOf ? lastNo + 1 : null;

        var label = MatchLabel(m);
        var streamDtos = OrderStreams(streams).Select(s => ToDto(s, label)).ToList();
        var version = Hash(m.Status + "|" + string.Join(";", games.Select(g => $"{g.GameNo}:{g.ScoreA}:{g.ScoreB}"))
                           + "|" + string.Join(";", streamDtos.Select(s => $"{s.LivestreamId}:{s.Status}:{s.StreamUrl}")));

        return new MatchLiveDto(
            m.MatchId, m.TournamentId, m.Tournament.Name,
            m.TeamAid, m.TeamA?.Name ?? "Chưa xác định", m.TeamBid, m.TeamB?.Name ?? "Chưa xác định",
            m.BestOf, m.Status, StatusText.Match(m.Status), m.ScheduledAt,
            winsA, winsB, leader, decided,
            closed is null, closed, CanScore(m, viewerAccountId), next,
            games.Select(g => new LiveGameDto(g.GameNo, g.ScoreA, g.ScoreB, g.WinnerTeamId, g.UpdatedAt)).ToList(),
            streamDtos, version);
    }

    // ===================== UC-46 livestream =====================

    public async Task<Result<TournamentLiveDto>> GetTournamentStreamsAsync(int tournamentId, int? viewerAccountId)
    {
        // Giải + livestream của giải trong một câu SQL (database ở xa, mỗi câu ~200 ms).
        var t = await _uow.Repository<Tournament>().Query().AsNoTracking().Include(x => x.Livestreams)
            .FirstOrDefaultAsync(x => x.TournamentId == tournamentId);
        if (t is null || !CanView(t, viewerAccountId))
            return Result<TournamentLiveDto>.Failure("Không tìm thấy giải đấu hoặc giải chưa được công bố.", StatusCodes.Status404NotFound);

        var matches = await _uow.Repository<Match>().Query().AsNoTracking()
            .Include(m => m.TeamA).Include(m => m.TeamB)
            .Where(m => m.TournamentId == tournamentId).ToListAsync();
        var labels = matches.ToDictionary(m => m.MatchId, MatchLabel);
        var streams = t.Livestreams.ToList();

        bool canManage = viewerAccountId.HasValue && t.OrganizerAccountId == viewerAccountId;
        var options = canManage
            ? matches.Where(m => m.TeamAid != null || m.TeamBid != null)
                .OrderBy(m => m.ScheduledAt ?? DateTime.MaxValue).ThenBy(m => m.MatchId)
                .Select(m => new LiveMatchOption(m.MatchId, labels[m.MatchId], StatusText.Match(m.Status))).ToList()
            : new List<LiveMatchOption>();

        return Result<TournamentLiveDto>.Success(new TournamentLiveDto(
            t.TournamentId, t.Name, t.Status, canManage,
            OrderStreams(streams).Select(s => ToDto(s, s.MatchId is int id && labels.TryGetValue(id, out var l) ? l : null)).ToList(),
            options));
    }

    public async Task<Result<LiveStreamDto>> CreateStreamAsync(int tournamentId, int actorAccountId, SaveLivestreamRequest request)
    {
        var (_, error) = await LoadForStreamManageAsync(tournamentId, actorAccountId);
        if (error is not null) return error;

        var (fields, fieldError) = await ValidateStreamAsync(tournamentId, null, request);
        if (fieldError is not null) return fieldError;

        var stream = new Livestream
        {
            TournamentId = tournamentId,
            MatchId = fields!.MatchId,
            Platform = fields.Platform,
            StreamUrl = fields.Url,
            Title = fields.Title,
            Status = fields.Status,
            CreatedByAccountId = actorAccountId,
            CreatedAt = DateTime.UtcNow
        };
        await _uow.Repository<Livestream>().AddAsync(stream);
        await _uow.SaveChangesAsync();
        return Result<LiveStreamDto>.Success(ToDto(stream, fields.MatchLabel), "Đã thêm livestream.");
    }

    public async Task<Result<LiveStreamDto>> UpdateStreamAsync(int tournamentId, int livestreamId, int actorAccountId, SaveLivestreamRequest request)
    {
        var (t, error) = await LoadForStreamManageAsync(tournamentId, actorAccountId);
        if (error is not null) return error;

        var stream = await _uow.Repository<Livestream>().Query()
            .FirstOrDefaultAsync(l => l.LivestreamId == livestreamId && l.TournamentId == tournamentId);
        if (stream is null) return Result<LiveStreamDto>.Failure("Không tìm thấy livestream trong giải này.", StatusCodes.Status404NotFound);

        var (fields, fieldError) = await ValidateStreamAsync(tournamentId, livestreamId, request);
        if (fieldError is not null) return fieldError;

        stream.MatchId = fields!.MatchId;
        stream.Platform = fields.Platform;
        stream.StreamUrl = fields.Url;
        stream.Title = fields.Title;
        stream.Status = fields.Status;
        _uow.Repository<Livestream>().Update(stream);
        // Người khác vừa gỡ đúng livestream này giữa lúc đọc và lưu (góp ý 25/11): báo rõ thay vì lỗi máy chủ.
        try { await _uow.SaveChangesAsync(); }
        catch (DbUpdateConcurrencyException)
        {
            return Result<LiveStreamDto>.Failure(StreamGoneMessage, StatusCodes.Status404NotFound);
        }
        return Result<LiveStreamDto>.Success(ToDto(stream, fields.MatchLabel), "Đã cập nhật livestream.");
    }

    public async Task<Result> DeleteStreamAsync(int tournamentId, int livestreamId, int actorAccountId)
    {
        var t = await _uow.Repository<Tournament>().Query().AsNoTracking().FirstOrDefaultAsync(x => x.TournamentId == tournamentId);
        if (t is null) return Result.Failure("Không tìm thấy giải đấu.", StatusCodes.Status404NotFound);
        if (t.OrganizerAccountId != actorAccountId)
            return Result.Failure("Chỉ Ban tổ chức của giải mới được quản lý livestream.", StatusCodes.Status403Forbidden);

        var stream = await _uow.Repository<Livestream>().Query()
            .FirstOrDefaultAsync(l => l.LivestreamId == livestreamId && l.TournamentId == tournamentId);
        if (stream is null) return Result.Failure("Không tìm thấy livestream trong giải này.", StatusCodes.Status404NotFound);

        _uow.Repository<Livestream>().Remove(stream);
        // Hai người (hoặc hai tab) cùng gỡ một livestream: lần sau không còn dòng nào để xóa.
        try { await _uow.SaveChangesAsync(); }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure(StreamGoneMessage, StatusCodes.Status404NotFound);
        }
        return Result.Success("Đã xóa livestream.");
    }

    public const string StreamGoneMessage = "Livestream này vừa bị gỡ khỏi giải. Vui lòng tải lại trang.";

    private sealed record StreamFields(string Url, string Platform, string? Title, int? MatchId, string? MatchLabel, string Status);

    private async Task<(Tournament? T, Result<LiveStreamDto>? Error)> LoadForStreamManageAsync(int tournamentId, int actorAccountId)
    {
        var t = await _uow.Repository<Tournament>().Query().AsNoTracking().FirstOrDefaultAsync(x => x.TournamentId == tournamentId);
        if (t is null) return (null, Result<LiveStreamDto>.Failure("Không tìm thấy giải đấu.", StatusCodes.Status404NotFound));
        if (t.OrganizerAccountId != actorAccountId)
            return (null, Result<LiveStreamDto>.Failure("Chỉ Ban tổ chức của giải mới được quản lý livestream.", StatusCodes.Status403Forbidden));
        if (t.Status == "CANCELLED")
            return (null, Result<LiveStreamDto>.Failure("Giải đã hủy, không thêm hoặc sửa livestream được."));
        return (t, null);
    }

    private async Task<(StreamFields? Fields, Result<LiveStreamDto>? Error)> ValidateStreamAsync(int tournamentId, int? selfId, SaveLivestreamRequest r)
    {
        var urlError = StreamEmbed.Validate(r.StreamUrl, out var uri);
        if (urlError is not null) return (null, Result<LiveStreamDto>.FieldFailure("StreamUrl", urlError));
        var url = r.StreamUrl!.Trim(); // lưu đúng link Ban tổ chức dán (đã kiểm tra hợp lệ ở trên)

        var title = string.IsNullOrWhiteSpace(r.Title) ? null : r.Title.Trim();
        if (title is { Length: > MaxTitleLength })
            return (null, Result<LiveStreamDto>.FieldFailure("Title", $"Tiêu đề tối đa {MaxTitleLength} ký tự."));

        var status = string.IsNullOrWhiteSpace(r.Status) ? "SCHEDULED" : r.Status.Trim().ToUpperInvariant();
        if (!StreamStatuses.Contains(status))
            return (null, Result<LiveStreamDto>.FieldFailure("Status", "Trạng thái livestream không hợp lệ."));

        string? matchLabel = null;
        if (r.MatchId is int matchId)
        {
            var match = await _uow.Repository<Match>().Query().AsNoTracking()
                .Include(m => m.TeamA).Include(m => m.TeamB)
                .FirstOrDefaultAsync(m => m.MatchId == matchId);
            if (match is null || match.TournamentId != tournamentId)
                return (null, Result<LiveStreamDto>.FieldFailure("MatchId", "Trận được gán không thuộc giải này."));
            matchLabel = MatchLabel(match);
        }

        if (await _uow.Repository<Livestream>().AnyAsync(l => l.TournamentId == tournamentId && l.StreamUrl == url && l.LivestreamId != (selfId ?? 0)))
            return (null, Result<LiveStreamDto>.FieldFailure("StreamUrl", "Link này đã có trong danh sách livestream của giải."));

        return (new StreamFields(url, StreamEmbed.DetectPlatform(uri!), title, r.MatchId, matchLabel, status), null);
    }

    // ===================== dùng chung =====================

    // Giải Nháp chỉ Ban tổ chức của giải xem được — giống trang chi tiết giải (POR-03).
    private static bool CanView(Tournament t, int? viewerAccountId)
        => t.Status != "DRAFT" || (viewerAccountId.HasValue && t.OrganizerAccountId == viewerAccountId);

    private static IEnumerable<Livestream> OrderStreams(IEnumerable<Livestream> streams) => streams
        .OrderBy(s => s.Status switch { "LIVE" => 0, "SCHEDULED" => 1, _ => 2 })
        .ThenByDescending(s => s.CreatedAt).ThenByDescending(s => s.LivestreamId);

    private static LiveStreamDto ToDto(Livestream s, string? matchLabel) => new(
        s.LivestreamId, s.MatchId, matchLabel, s.Platform, StreamEmbed.PlatformLabel(s.Platform, s.StreamUrl), s.StreamUrl,
        StreamEmbed.EmbedUrl(s.Platform, s.StreamUrl), s.Title, s.Status, StreamEmbed.StatusLabel(s.Status), s.CreatedAt,
        StreamEmbed.EmbedKind(s.Platform, s.StreamUrl));

    private static string MatchLabel(Match m)
    {
        var label = $"{m.TeamA?.Name ?? "Chưa xác định"} vs {m.TeamB?.Name ?? "Chưa xác định"}";
        return m.ScheduledAt is DateTime at ? $"{label} · {at.AddHours(7):HH:mm dd/MM}" : label;
    }

    private static string Hash(string text)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..16];
}
