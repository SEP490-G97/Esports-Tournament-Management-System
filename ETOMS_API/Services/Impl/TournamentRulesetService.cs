using System.Data.Common;
using ETOMS_API.DTOs.Tournament;
using ETOMS_API.Models;
using ETOMS_API.Repositories;
using ETOMS_API.Repositories.Interface;
using ETOMS_API.Services.Interface;
using ETOMS_API.Utils;
using Microsoft.EntityFrameworkCore;

namespace ETOMS_API.Services.Impl;

// Module GRL-06. Liên kết giải - bộ luật nằm ở bảng TournamentRuleset (chưa có trong database chung).
// Bộ luật đã chọn là nguồn vị trí cho đội hình (GRL-04). Số người của giải vẫn là cột của Tournament
// (TOUR-02), ở đây chỉ kiểm tra nó nằm trong giới hạn của bộ luật.
public class TournamentRulesetService : ITournamentRulesetService
{
    private const string TableMissingMessage = "Không truy cập được dữ liệu bộ luật. Có thể bảng GameRuleset hoặc TournamentRuleset chưa được tạo trong database.";

    private readonly IUnitOfWork _uow;

    public TournamentRulesetService(IUnitOfWork uow) => _uow = uow;

    public async Task<int?> GetOrganizerAccountIdAsync(int tournamentId)
        => (await _uow.Repository<Tournament>().GetByIdAsync(tournamentId))?.OrganizerAccountId;

    public async Task<Result<TournamentRulesetBoardDto>> GetBoardAsync(int tournamentId)
    {
        try
        {
            var board = await BuildBoardAsync(tournamentId);
            return board is null
                ? Result<TournamentRulesetBoardDto>.Failure("Không tìm thấy giải đấu.")
                : Result<TournamentRulesetBoardDto>.Success(board);
        }
        catch (DbException)
        {
            return Result<TournamentRulesetBoardDto>.Failure(TableMissingMessage);
        }
    }

    // ===== GRL-06: Chọn bộ luật cho giải =====
    public async Task<Result<TournamentRulesetBoardDto>> SelectRulesetAsync(int tournamentId, int gameRulesetId)
    {
        var tournament = await _uow.Repository<Tournament>().GetByIdAsync(tournamentId);
        if (tournament is null) return Result<TournamentRulesetBoardDto>.Failure("Không tìm thấy giải đấu.");

        try
        {
            var blocked = await BlockedReasonAsync(tournament);
            if (blocked is not null) return Result<TournamentRulesetBoardDto>.Failure(blocked);

            var ruleset = await _uow.Repository<GameRuleset>().Query()
                .FirstOrDefaultAsync(r => r.GameRulesetId == gameRulesetId && r.GameId == tournament.GameId);
            if (ruleset is null) return Result<TournamentRulesetBoardDto>.Failure("Bộ luật không thuộc tựa game của giải.");

            var link = await _uow.Repository<TournamentRuleset>().Query()
                .FirstOrDefaultAsync(tr => tr.TournamentId == tournamentId);
            if (link?.GameRulesetId == gameRulesetId)
                return Result<TournamentRulesetBoardDto>.Success((await BuildBoardAsync(tournamentId))!, "Giải đã dùng bộ luật này.");

            if (ruleset.Status != "ACTIVE")
                return Result<TournamentRulesetBoardDto>.Failure("Bộ luật này đã bị loại, vui lòng chọn bộ luật khác.");

            var mismatch = Mismatch(ruleset, tournament);
            if (mismatch is not null) return Result<TournamentRulesetBoardDto>.Failure(mismatch);

            if (link is null)
                await _uow.Repository<TournamentRuleset>().AddAsync(new TournamentRuleset
                {
                    TournamentId = tournamentId,
                    GameRulesetId = gameRulesetId,
                    CreatedAt = DateTime.UtcNow
                });
            else
                link.GameRulesetId = gameRulesetId;

            await _uow.SaveChangesAsync();
            return Result<TournamentRulesetBoardDto>.Success((await BuildBoardAsync(tournamentId))!, "Đã chọn bộ luật cho giải.");
        }
        catch (Exception ex) when (ex is DbException or DbUpdateException)
        {
            return Result<TournamentRulesetBoardDto>.Failure(TableMissingMessage);
        }
    }

    private async Task<TournamentRulesetBoardDto?> BuildBoardAsync(int tournamentId)
    {
        var tournament = await _uow.Repository<Tournament>().GetByIdAsync(tournamentId);
        if (tournament is null) return null;

        var gameTitle = await _uow.Repository<Game>().Query()
            .Where(g => g.GameId == tournament.GameId).Select(g => g.Title).FirstOrDefaultAsync() ?? string.Empty;

        var selectedId = await _uow.Repository<TournamentRuleset>().Query()
            .Where(tr => tr.TournamentId == tournamentId)
            .Select(tr => (int?)tr.GameRulesetId)
            .FirstOrDefaultAsync();

        // Bộ luật đã loại vẫn hiện nếu giải đang dùng, để thấy rõ giải đang theo luật nào.
        var rulesets = await _uow.Repository<GameRuleset>().Query()
            .Where(r => r.GameId == tournament.GameId && (r.Status == "ACTIVE" || r.GameRulesetId == selectedId))
            .OrderBy(r => r.Name)
            .ToListAsync();

        var blocked = await BlockedReasonAsync(tournament);
        return new TournamentRulesetBoardDto
        {
            TournamentId = tournamentId,
            TournamentName = tournament.Name,
            GameTitle = gameTitle,
            StarterCount = tournament.StarterCount,
            MaxSubstitutes = tournament.MaxSubstitutes,
            SelectedRulesetId = selectedId,
            CanEdit = blocked is null,
            BlockedReason = blocked,
            Options = rulesets.Select(r =>
            {
                var mismatch = Mismatch(r, tournament);
                return new RulesetOptionDto
                {
                    GameRulesetId = r.GameRulesetId,
                    Name = r.Name,
                    MinStarters = r.MinStarters,
                    MaxStarters = r.MaxStarters,
                    MaxSubstitutes = r.MaxSubstitutes,
                    DefaultFormat = r.DefaultFormat,
                    IsActive = r.Status == "ACTIVE",
                    FitsTournament = mismatch is null,
                    MismatchReason = mismatch
                };
            }).ToList()
        };
    }

    // Đổi bộ luật khi đã có đơn đăng ký sẽ làm đội hình đã khai sai luật, nên chỉ cho đổi trước khi có đơn.
    private async Task<string?> BlockedReasonAsync(Tournament t)
    {
        if (t.Status is not ("DRAFT" or "PUBLISHED")) return "Giải đã bắt đầu, kết thúc hoặc đã hủy nên không đổi được bộ luật.";
        var hasRegistration = await _uow.Repository<Registration>().AnyAsync(r => r.TournamentId == t.TournamentId);
        return hasRegistration ? "Giải đã có đơn đăng ký nên không đổi được bộ luật." : null;
    }

    private static string? Mismatch(GameRuleset r, Tournament t)
    {
        if (t.StarterCount < r.MinStarters || t.StarterCount > r.MaxStarters)
            return r.MinStarters == r.MaxStarters
                ? $"Bộ luật yêu cầu {r.MinStarters} người chính thức, giải đang đặt {t.StarterCount}."
                : $"Bộ luật yêu cầu {r.MinStarters}–{r.MaxStarters} người chính thức, giải đang đặt {t.StarterCount}.";
        if (t.MaxSubstitutes > r.MaxSubstitutes)
            return $"Bộ luật cho tối đa {r.MaxSubstitutes} dự bị, giải đang đặt {t.MaxSubstitutes}.";
        return null;
    }
}
