using System.Data.Common;
using ETOMS_API.DTOs.PickBan;
using ETOMS_API.Models;
using ETOMS_API.Repositories;
using ETOMS_API.Services.Interface;
using ETOMS_API.Utils;
using Microsoft.EntityFrameworkCore;

namespace ETOMS_API.Services.Impl;

// Module GRL-07. Hai đội cấm/chọn bản đồ bên ngoài hệ thống, trọng tài nhập lại kết quả.
// Bản đồ lấy từ bộ luật giải đã chọn (GRL-06, GRL-02). Kết quả công khai và là bằng chứng khi tranh chấp,
// nên khóa lại từ lúc trận bị tranh chấp hoặc đã có kết quả.
public class MatchPickBanService : IMatchPickBanService
{
    private const string TableMissingMessage = "Không truy cập được dữ liệu cấm chọn bản đồ. Có thể bảng MatchPickBan, GameMap hoặc TournamentRuleset chưa được tạo trong database.";
    private static readonly string[] Actions = { "BAN", "PICK", "DECIDER" };

    private readonly IUnitOfWork _uow;

    public MatchPickBanService(IUnitOfWork uow) => _uow = uow;

    public async Task<bool?> CanRecordAsync(int matchId, int accountId)
    {
        var match = await _uow.Repository<Match>().GetByIdAsync(matchId);
        if (match is null) return null;
        if (match.RefereeAccountId == accountId) return true;

        var organizerId = await _uow.Repository<Tournament>().Query()
            .Where(t => t.TournamentId == match.TournamentId)
            .Select(t => t.OrganizerAccountId)
            .FirstOrDefaultAsync();
        return organizerId == accountId;
    }

    public async Task<Result<MatchPickBanBoardDto>> GetBoardAsync(int matchId, int? viewerAccountId)
    {
        try
        {
            var board = await BuildBoardAsync(matchId, viewerAccountId);
            return board is null
                ? Result<MatchPickBanBoardDto>.Failure("Không tìm thấy trận đấu.")
                : Result<MatchPickBanBoardDto>.Success(board);
        }
        catch (DbException)
        {
            return Result<MatchPickBanBoardDto>.Failure(TableMissingMessage);
        }
    }

    // ===== GRL-07: Nhập kết quả pick-ban =====
    public async Task<Result<MatchPickBanBoardDto>> SaveAsync(int matchId, int accountId, SaveMatchPickBanRequest request)
    {
        var match = await _uow.Repository<Match>().GetByIdAsync(matchId);
        if (match is null) return Result<MatchPickBanBoardDto>.Failure("Không tìm thấy trận đấu.");

        try
        {
            var rulesetId = await RulesetIdOfAsync(match.TournamentId);
            var maps = await MapsOfAsync(rulesetId);
            var blocked = BlockedReason(match, rulesetId, maps);
            if (blocked is not null) return Result<MatchPickBanBoardDto>.Failure(blocked);

            var existing = await _uow.Repository<MatchPickBan>().Query()
                .Where(p => p.MatchId == matchId)
                .OrderBy(p => p.StepNo)
                .ToListAsync();
            var savedMapIds = existing.Select(p => p.GameMapId).ToHashSet();

            var steps = request.Steps ?? new List<PickBanStepInput>();
            var mapById = maps.ToDictionary(m => m.GameMapId);
            var used = new HashSet<int>();
            var rows = new List<(string Action, int? TeamId, int MapId)>();

            for (var i = 0; i < steps.Count; i++)
            {
                var no = i + 1;
                var action = (steps[i].Action ?? string.Empty).Trim().ToUpperInvariant();
                if (!Actions.Contains(action))
                    return Result<MatchPickBanBoardDto>.Failure($"Lượt {no}: loại lượt không hợp lệ.");

                int? teamId = null;
                if (action == "DECIDER")
                {
                    if (no != steps.Count)
                        return Result<MatchPickBanBoardDto>.Failure("Bản đồ còn lại phải là lượt cuối cùng.");
                }
                else
                {
                    var side = (steps[i].Side ?? string.Empty).Trim().ToUpperInvariant();
                    if (side is not ("A" or "B"))
                        return Result<MatchPickBanBoardDto>.Failure($"Lượt {no}: vui lòng chọn đội {(action == "BAN" ? "cấm" : "chọn")}.");
                    teamId = side == "A" ? match.TeamAid : match.TeamBid;
                }

                if (!mapById.TryGetValue(steps[i].GameMapId, out var map))
                    return Result<MatchPickBanBoardDto>.Failure($"Lượt {no}: bản đồ không thuộc bộ luật của giải.");
                if (map.Status != "ACTIVE" && !savedMapIds.Contains(map.GameMapId))
                    return Result<MatchPickBanBoardDto>.Failure($"Lượt {no}: bản đồ {map.Name} đã bị loại.");
                if (!used.Add(map.GameMapId))
                    return Result<MatchPickBanBoardDto>.Failure($"Bản đồ {map.Name} xuất hiện ở hai lượt.");

                rows.Add((action, teamId, map.GameMapId));
            }

            var played = rows.Count(r => r.Action != "BAN");
            if (played > match.BestOf)
                return Result<MatchPickBanBoardDto>.Failure($"Trận BO{match.BestOf} đấu tối đa {match.BestOf} ván nhưng có {played} bản đồ được chọn.");

            // Sửa tại chỗ theo số lượt để không vướng khóa duy nhất (MatchID, StepNo) khi xóa rồi thêm.
            var now = DateTime.UtcNow;
            for (var i = 0; i < rows.Count; i++)
            {
                var row = i < existing.Count ? existing[i] : null;
                if (row is null)
                {
                    row = new MatchPickBan { MatchId = matchId, StepNo = i + 1 };
                    await _uow.Repository<MatchPickBan>().AddAsync(row);
                }
                row.Action = rows[i].Action;
                row.TeamId = rows[i].TeamId;
                row.GameMapId = rows[i].MapId;
                row.RecordedByAccountId = accountId;
                row.RecordedAt = now;
            }
            foreach (var extra in existing.Skip(rows.Count))
                _uow.Repository<MatchPickBan>().Remove(extra);

            await _uow.SaveChangesAsync();
            var message = rows.Count == 0 ? "Đã xóa kết quả cấm chọn bản đồ." : "Đã lưu kết quả cấm chọn bản đồ.";
            return Result<MatchPickBanBoardDto>.Success((await BuildBoardAsync(matchId, accountId))!, message);
        }
        catch (Exception ex) when (ex is DbException or DbUpdateException)
        {
            return Result<MatchPickBanBoardDto>.Failure(TableMissingMessage);
        }
    }

    private async Task<MatchPickBanBoardDto?> BuildBoardAsync(int matchId, int? viewerAccountId)
    {
        var match = await _uow.Repository<Match>().GetByIdAsync(matchId);
        if (match is null) return null;

        var tournamentName = await _uow.Repository<Tournament>().Query()
            .Where(t => t.TournamentId == match.TournamentId)
            .Select(t => t.Name)
            .FirstOrDefaultAsync() ?? string.Empty;

        var teamIds = new[] { match.TeamAid, match.TeamBid }.Where(id => id.HasValue).Select(id => id!.Value).ToList();
        var teamNames = await _uow.Repository<Team>().Query()
            .Where(t => teamIds.Contains(t.TeamId))
            .ToDictionaryAsync(t => t.TeamId, t => t.Name);
        string NameOf(int? teamId) => teamId.HasValue && teamNames.TryGetValue(teamId.Value, out var n) ? n : "Chưa xác định";

        var rulesetId = await RulesetIdOfAsync(match.TournamentId);
        var rulesetName = rulesetId is null ? null : await _uow.Repository<GameRuleset>().Query()
            .Where(r => r.GameRulesetId == rulesetId)
            .Select(r => r.Name)
            .FirstOrDefaultAsync();

        var saved = await _uow.Repository<MatchPickBan>().Query()
            .Where(p => p.MatchId == matchId)
            .OrderBy(p => p.StepNo)
            .ToListAsync();
        var savedMapIds = saved.Select(p => p.GameMapId).ToHashSet();

        // Bản đồ đã loại vẫn hiện nếu trận này đã dùng, để kết quả cũ đọc được.
        var maps = (await MapsOfAsync(rulesetId))
            .Where(m => m.Status == "ACTIVE" || savedMapIds.Contains(m.GameMapId))
            .OrderBy(m => m.Status == "ACTIVE" ? 0 : 1).ThenBy(m => m.Name)
            .ToList();
        var mapNames = maps.ToDictionary(m => m.GameMapId, m => m.Name);

        var steps = saved.Select(p => new PickBanStepDto
        {
            StepNo = p.StepNo,
            Action = p.Action,
            ActionLabel = p.Action switch { "BAN" => "Cấm", "PICK" => "Chọn", _ => "Bản đồ còn lại" },
            Side = p.TeamId is null ? null : p.TeamId == match.TeamAid ? "A" : p.TeamId == match.TeamBid ? "B" : null,
            TeamName = p.TeamId is null ? null : NameOf(p.TeamId),
            GameMapId = p.GameMapId,
            MapName = mapNames.TryGetValue(p.GameMapId, out var mn) ? mn : "Bản đồ #" + p.GameMapId
        }).ToList();

        var games = steps.Where(s => s.Action != "BAN")
            .Select((s, i) => new PickBanGameDto
            {
                GameNo = i + 1,
                MapName = s.MapName,
                PickedBy = s.Action == "DECIDER" ? "Bản đồ còn lại" : s.TeamName + " chọn"
            }).ToList();

        string? recordedBy = null;
        DateTime? recordedAt = null;
        if (saved.Count > 0)
        {
            var last = saved.OrderByDescending(p => p.RecordedAt).First();
            recordedAt = last.RecordedAt;
            recordedBy = await _uow.Repository<Account>().Query()
                .Where(a => a.AccountId == last.RecordedByAccountId)
                .Select(a => a.FullName)
                .FirstOrDefaultAsync();
        }

        var canRecord = viewerAccountId.HasValue && await CanRecordAsync(matchId, viewerAccountId.Value) == true;
        var blocked = BlockedReason(match, rulesetId, maps);

        return new MatchPickBanBoardDto
        {
            MatchId = match.MatchId,
            TournamentId = match.TournamentId,
            TournamentName = tournamentName,
            RulesetName = rulesetName,
            TeamAName = NameOf(match.TeamAid),
            TeamBName = NameOf(match.TeamBid),
            BestOf = match.BestOf,
            StatusLabel = StatusText.Match(match.Status),
            StatusClass = StatusText.MatchClass(match.Status),
            CanEdit = canRecord && blocked is null,
            BlockedReason = canRecord ? blocked : null,
            Maps = maps.Select(m => new PickBanMapDto
            {
                GameMapId = m.GameMapId,
                Name = m.Name,
                ImageUrl = m.ImageUrl,
                IsActive = m.Status == "ACTIVE"
            }).ToList(),
            Steps = steps,
            Games = games,
            RecordedByName = recordedBy,
            RecordedAt = recordedAt
        };
    }

    private async Task<int?> RulesetIdOfAsync(int tournamentId)
        => await _uow.Repository<TournamentRuleset>().Query()
            .Where(tr => tr.TournamentId == tournamentId)
            .Select(tr => (int?)tr.GameRulesetId)
            .FirstOrDefaultAsync();

    private async Task<List<GameMap>> MapsOfAsync(int? rulesetId)
        => rulesetId is null
            ? new List<GameMap>()
            : await _uow.Repository<GameMap>().Query().Where(m => m.GameRulesetId == rulesetId).ToListAsync();

    private static string? BlockedReason(Match match, int? rulesetId, List<GameMap> maps)
    {
        if (match.Status == "DISPUTED")
            return "Trận đang tranh chấp nên kết quả cấm chọn được giữ nguyên làm bằng chứng.";
        if (match.Status is "VERIFIED" or "WALKOVER" or "CANCELLED")
            return "Trận đã có kết quả hoặc đã hủy nên không sửa được kết quả cấm chọn.";
        if (match.TeamAid is null || match.TeamBid is null)
            return "Trận chưa đủ hai đội.";
        if (rulesetId is null)
            return "Giải chưa chọn bộ luật nên chưa có danh sách bản đồ.";
        if (!maps.Any(m => m.Status == "ACTIVE"))
            return "Bộ luật của giải chưa có bản đồ nào còn dùng.";
        return null;
    }
}
