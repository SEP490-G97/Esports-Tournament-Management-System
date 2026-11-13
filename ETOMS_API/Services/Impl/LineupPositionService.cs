using System.Data.Common;
using ETOMS_API.DTOs.Registration;
using ETOMS_API.Models;
using ETOMS_API.Repositories;
using ETOMS_API.Repositories.Interface;
using ETOMS_API.Services.Interface;
using ETOMS_API.Utils;
using Microsoft.EntityFrameworkCore;

namespace ETOMS_API.Services.Impl;

// Module GRL-04. Chỉ gán vị trí cho đội hình đã khai; thêm bớt người trong đội hình là việc của
// form đăng ký (REG-02). Vị trí lấy từ GRL-03, theo bộ luật giải đã chọn ở GRL-06.
public class LineupPositionService : ILineupPositionService
{
    private const string SchemaMissingMessage = "Không truy cập được dữ liệu vị trí. Có thể bảng GameRuleset, TournamentRuleset, GamePosition hoặc cột RegistrationLineup.GamePositionID chưa được tạo trong database.";
    private const string NoRulesetMessage = "Giải chưa chọn bộ luật nên chưa chọn được vị trí.";
    private const string NoPositionMessage = "Bộ luật của giải chưa khai vị trí nên không cần chọn.";
    private static readonly string[] EditableStatuses = { "DRAFT", "PENDING", "APPROVED", "WAITLISTED" };

    private readonly IUnitOfWork _uow;

    public LineupPositionService(IUnitOfWork uow) => _uow = uow;

    public async Task<int?> GetCaptainAccountIdAsync(int registrationId)
        => await _uow.Repository<Registration>().Query()
            .Where(r => r.RegistrationId == registrationId)
            .Select(r => (int?)r.Team.CaptainAccountId)
            .FirstOrDefaultAsync();

    public async Task<Result<LineupPositionBoardDto>> GetBoardAsync(int registrationId)
    {
        if (!PendingSchema.LineupGamePosition) return Result<LineupPositionBoardDto>.Failure(SchemaMissingMessage);
        try
        {
            var board = await BuildBoardAsync(registrationId);
            return board is null
                ? Result<LineupPositionBoardDto>.Failure("Không tìm thấy đơn đăng ký.")
                : Result<LineupPositionBoardDto>.Success(board);
        }
        catch (DbException)
        {
            return Result<LineupPositionBoardDto>.Failure(SchemaMissingMessage);
        }
    }

    // ===== GRL-04: Lưu vị trí của đội hình =====
    public async Task<Result<LineupPositionBoardDto>> SavePositionsAsync(int registrationId, SaveLineupPositionsRequest request)
    {
        if (!PendingSchema.LineupGamePosition) return Result<LineupPositionBoardDto>.Failure(SchemaMissingMessage);
        try
        {
            var registration = await _uow.Repository<Registration>().Query()
                .Include(r => r.Tournament)
                .Include(r => r.RegistrationLineups)
                .FirstOrDefaultAsync(r => r.RegistrationId == registrationId);
            if (registration is null) return Result<LineupPositionBoardDto>.Failure("Không tìm thấy đơn đăng ký.");

            var blocked = BlockedReason(registration, registration.Tournament);
            if (blocked is not null) return Result<LineupPositionBoardDto>.Failure(blocked);

            var rulesetId = await RulesetIdOfAsync(registration.TournamentId);
            if (rulesetId is null) return Result<LineupPositionBoardDto>.Failure(NoRulesetMessage);

            var positions = await _uow.Repository<GamePosition>().Query()
                .Where(p => p.GameRulesetId == rulesetId)
                .ToListAsync();
            if (!positions.Any(p => p.Status == "ACTIVE"))
                return Result<LineupPositionBoardDto>.Failure(NoPositionMessage);

            var lineups = registration.RegistrationLineups.ToDictionary(l => l.RegistrationLineupId);
            var chosen = lineups.ToDictionary(kv => kv.Key, kv => kv.Value.GamePositionId);
            foreach (var item in request.Items)
            {
                if (!lineups.TryGetValue(item.RegistrationLineupId, out var lineup))
                    return Result<LineupPositionBoardDto>.Failure("Có người không thuộc đội hình của đơn này.");

                if (item.GamePositionId is int positionId && positionId != lineup.GamePositionId)
                {
                    var position = positions.FirstOrDefault(p => p.GamePositionId == positionId);
                    if (position is null) return Result<LineupPositionBoardDto>.Failure("Vị trí không thuộc bộ luật của giải.");
                    if (position.Status != "ACTIVE") return Result<LineupPositionBoardDto>.Failure($"Vị trí {position.Name} đã bị loại, vui lòng chọn vị trí khác.");
                }
                chosen[item.RegistrationLineupId] = item.GamePositionId;
            }

            var starters = lineups.Values.Where(l => l.Slot == "STARTER").ToList();
            var starterPositions = starters.Select(l => chosen[l.RegistrationLineupId]).ToList();
            if (StarterPositionRequired(positions, starters.Count) && starterPositions.Any(p => p is null))
                return Result<LineupPositionBoardDto>.Failure("Mỗi người chính thức phải có một vị trí.");

            var duplicate = starterPositions.Where(p => p is not null).GroupBy(p => p).FirstOrDefault(g => g.Count() > 1);
            if (duplicate is not null)
            {
                var name = positions.First(p => p.GamePositionId == duplicate.Key).Name;
                return Result<LineupPositionBoardDto>.Failure($"Hai người chính thức không được cùng vị trí {name}.");
            }

            foreach (var (lineupId, positionId) in chosen) lineups[lineupId].GamePositionId = positionId;
            await _uow.SaveChangesAsync();

            return Result<LineupPositionBoardDto>.Success((await BuildBoardAsync(registrationId))!, "Đã lưu vị trí đội hình.");
        }
        catch (Exception ex) when (ex is DbException or DbUpdateException)
        {
            return Result<LineupPositionBoardDto>.Failure(SchemaMissingMessage);
        }
    }

    private async Task<LineupPositionBoardDto?> BuildBoardAsync(int registrationId)
    {
        var registration = await _uow.Repository<Registration>().Query()
            .Where(r => r.RegistrationId == registrationId)
            .Select(r => new
            {
                Registration = r,
                r.Tournament,
                TeamName = r.Team.Name,
                GameTitle = r.Tournament.Game.Title
            })
            .FirstOrDefaultAsync();
        if (registration is null) return null;

        var members = await _uow.Repository<RegistrationLineup>().Query()
            .Where(l => l.RegistrationId == registrationId)
            .OrderBy(l => l.Slot == "STARTER" ? 0 : 1).ThenBy(l => l.RegistrationLineupId)
            .Select(l => new LineupMemberDto
            {
                RegistrationLineupId = l.RegistrationLineupId,
                DisplayName = l.InGameNameSnapshot ?? l.Account.FullName,
                Slot = l.Slot,
                SlotLabel = l.Slot == "STARTER" ? "Chính thức" : "Dự bị",
                GamePositionId = l.GamePositionId
            })
            .ToListAsync();

        var ruleset = await _uow.Repository<TournamentRuleset>().Query()
            .Where(tr => tr.TournamentId == registration.Tournament.TournamentId)
            .Select(tr => new { tr.GameRulesetId, tr.GameRuleset.Name })
            .FirstOrDefaultAsync();

        var positions = ruleset is null
            ? new List<GamePosition>()
            : await _uow.Repository<GamePosition>().Query()
                .Where(p => p.GameRulesetId == ruleset.GameRulesetId)
                .OrderBy(p => p.SortOrder).ThenBy(p => p.GamePositionId)
                .ToListAsync();

        // Vị trí đã loại vẫn hiện nếu còn người đang giữ, để không mất dữ liệu đã chọn.
        var assigned = members.Where(m => m.GamePositionId is not null).Select(m => m.GamePositionId!.Value).ToHashSet();
        var options = positions
            .Where(p => p.Status == "ACTIVE" || assigned.Contains(p.GamePositionId))
            .Select(p => new LineupPositionOptionDto { GamePositionId = p.GamePositionId, Name = p.Name, IsActive = p.Status == "ACTIVE" })
            .ToList();

        var blocked = BlockedReason(registration.Registration, registration.Tournament)
            ?? (ruleset is null ? NoRulesetMessage : null)
            ?? (positions.Any(p => p.Status == "ACTIVE") ? null : NoPositionMessage);

        return new LineupPositionBoardDto
        {
            RegistrationId = registrationId,
            TournamentName = registration.Tournament.Name,
            TeamName = registration.TeamName,
            GameTitle = registration.GameTitle,
            RulesetName = ruleset?.Name,
            RegistrationStatusLabel = StatusText.Registration(registration.Registration.Status),
            CanEdit = blocked is null,
            BlockedReason = blocked,
            StarterPositionRequired = StarterPositionRequired(positions, members.Count(m => m.Slot == "STARTER")),
            Positions = options,
            Members = members
        };
    }

    // Đơn còn hiệu lực và nhánh chưa khóa thì mới đổi vị trí, vì khóa nhánh là chốt đội hình (BR-BRK-02).
    private static string? BlockedReason(Registration registration, Tournament tournament)
    {
        if (!EditableStatuses.Contains(registration.Status)) return "Đơn đăng ký không còn hiệu lực nên không đổi được vị trí.";
        if (tournament.Status is "COMPLETED" or "CANCELLED") return "Giải đã kết thúc hoặc đã hủy.";
        return tournament.BracketLockedAt is null ? null : "Nhánh đấu đã khóa nên đội hình đã chốt.";
    }

    private Task<int?> RulesetIdOfAsync(int tournamentId)
        => _uow.Repository<TournamentRuleset>().Query()
            .Where(tr => tr.TournamentId == tournamentId)
            .Select(tr => (int?)tr.GameRulesetId)
            .FirstOrDefaultAsync();

    private static bool StarterPositionRequired(IEnumerable<GamePosition> positions, int starterCount)
        => starterCount > 0 && positions.Count(p => p.Status == "ACTIVE") >= starterCount;
}
