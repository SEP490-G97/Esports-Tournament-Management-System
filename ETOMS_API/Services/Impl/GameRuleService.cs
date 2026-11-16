using System.Data.Common;
using System.Text.RegularExpressions;
using ETOMS_API.DTOs.GameRule;
using ETOMS_API.Models;
using ETOMS_API.Repositories;
using ETOMS_API.Repositories.Interface;
using ETOMS_API.Services.Interface;
using ETOMS_API.Utils;
using Microsoft.EntityFrameworkCore;

namespace ETOMS_API.Services.Impl;

// Module GRL-01. Một tựa game có nhiều bộ luật (bảng GameRuleset). Các cột luật cũ trên bảng Game
// để nguyên cho ADM-03, module này không đọc và không sửa chúng.
// Bảng GameRuleset chưa chắc đã có trong database chung, nên lỗi truy cập bảng trả thành thông báo.
public class GameRuleService : IGameRuleService
{
    private const string TableMissingMessage = "Không truy cập được dữ liệu bộ luật. Có thể bảng GameRuleset hoặc TournamentRuleset chưa được tạo trong database.";
    private static readonly string[] Formats = { "SINGLE_ELIMINATION", "DOUBLE_ELIMINATION", "ROUND_ROBIN", "HYBRID" };
    private static readonly Regex ExtensionPattern = new("^[a-z0-9]{1,10}$", RegexOptions.Compiled);

    private readonly IUnitOfWork _uow;

    public GameRuleService(IUnitOfWork uow) => _uow = uow;

    public async Task<bool> IsAdminAsync(int accountId)
        => await _uow.Repository<Account>().AnyAsync(a => a.AccountId == accountId && a.Role == "ADMIN" && a.Status == "ACTIVE");

    public async Task<GameRuleBoardDto> GetBoardAsync()
    {
        var games = await _uow.Repository<Game>().Query().OrderBy(g => g.Title).ToListAsync();
        var board = new GameRuleBoardDto
        {
            Games = games.Select(g => new GameWithRulesetsDto { GameId = g.GameId, Title = g.Title, ShortCode = g.ShortCode, IsActive = g.Status == "ACTIVE" }).ToList()
        };

        try
        {
            var rulesets = await _uow.Repository<GameRuleset>().Query()
                .OrderBy(r => r.Status == "ACTIVE" ? 0 : 1).ThenBy(r => r.Name)
                .ToListAsync();
            var openCounts = await OpenTournamentCountsAsync(null);
            foreach (var game in board.Games)
                game.Rulesets = rulesets.Where(r => r.GameId == game.GameId)
                    .Select(r => ToDto(r, openCounts.GetValueOrDefault(r.GameRulesetId))).ToList();
        }
        catch (DbException)
        {
            board.RulesetError = TableMissingMessage;
        }
        return board;
    }

    // ===== GRL-01: Tạo bộ luật cho một tựa game =====
    public async Task<Result<GameRulesetDto>> CreateRulesetAsync(int gameId, SaveGameRulesetRequest request)
    {
        if (!await _uow.Repository<Game>().AnyAsync(g => g.GameId == gameId))
            return Result<GameRulesetDto>.Failure("Không tìm thấy tựa game.");

        var invalid = Validate(request, out var name, out var format, out var extensions);
        if (invalid is not null) return invalid;

        try
        {
            if (await NameTakenAsync(gameId, name, null))
                return Result<GameRulesetDto>.FieldFailure(nameof(request.Name), "Tựa game này đã có bộ luật cùng tên.");

            var ruleset = new GameRuleset
            {
                GameId = gameId,
                Name = name,
                MinStarters = request.MinStarters,
                MaxStarters = request.MaxStarters,
                MaxSubstitutes = request.MaxSubstitutes,
                DefaultFormat = format,
                AllowedEvidenceTypes = extensions,
                MaxEvidenceFileSizeMb = request.MaxEvidenceFileSizeMb,
                Status = "ACTIVE",
                CreatedAt = DateTime.UtcNow
            };
            await _uow.Repository<GameRuleset>().AddAsync(ruleset);
            await _uow.SaveChangesAsync();
            return Result<GameRulesetDto>.Success(ToDto(ruleset, 0), "Đã tạo bộ luật.");
        }
        catch (Exception ex) when (ex is DbException or DbUpdateException)
        {
            return Result<GameRulesetDto>.Failure(TableMissingMessage);
        }
    }

    // ===== GRL-01: Sửa bộ luật hoặc chuyển còn dùng / đã loại =====
    public async Task<Result<GameRulesetDto>> UpdateRulesetAsync(int rulesetId, SaveGameRulesetRequest request)
    {
        var invalid = Validate(request, out var name, out var format, out var extensions);
        if (invalid is not null) return invalid;

        var status = string.IsNullOrWhiteSpace(request.Status) ? null : request.Status.Trim().ToUpperInvariant();
        if (status is not null and not ("ACTIVE" or "RETIRED"))
            return Result<GameRulesetDto>.Failure("Trạng thái bộ luật không hợp lệ.");

        try
        {
            var ruleset = await _uow.Repository<GameRuleset>().Query().FirstOrDefaultAsync(r => r.GameRulesetId == rulesetId);
            if (ruleset is null) return Result<GameRulesetDto>.Failure("Không tìm thấy bộ luật.");

            if (await NameTakenAsync(ruleset.GameId, name, rulesetId))
                return Result<GameRulesetDto>.FieldFailure(nameof(request.Name), "Tựa game này đã có bộ luật cùng tên.");

            ruleset.Name = name;
            ruleset.MinStarters = request.MinStarters;
            ruleset.MaxStarters = request.MaxStarters;
            ruleset.MaxSubstitutes = request.MaxSubstitutes;
            ruleset.DefaultFormat = format;
            ruleset.AllowedEvidenceTypes = extensions;
            ruleset.MaxEvidenceFileSizeMb = request.MaxEvidenceFileSizeMb;
            if (status is not null) ruleset.Status = status;
            await _uow.SaveChangesAsync();

            var openCount = (await OpenTournamentCountsAsync(rulesetId)).GetValueOrDefault(rulesetId);
            var message = openCount > 0
                ? $"Đã lưu bộ luật. {openCount} giải chưa kết thúc đang dùng bộ luật này: số người và thể thức của giải giữ nguyên, còn bản đồ, vị trí và định dạng file minh chứng theo bộ luật mới."
                : "Đã lưu bộ luật.";
            return Result<GameRulesetDto>.Success(ToDto(ruleset, openCount), message);
        }
        catch (Exception ex) when (ex is DbException or DbUpdateException)
        {
            return Result<GameRulesetDto>.Failure(TableMissingMessage);
        }
    }

    private static Result<GameRulesetDto>? Validate(SaveGameRulesetRequest request, out string name, out string format, out string extensions)
    {
        name = request.Name.Trim();
        format = request.DefaultFormat.Trim().ToUpperInvariant();
        extensions = NormalizeExtensions(request.AllowedEvidenceTypes) ?? string.Empty;

        if (name.Length == 0)
            return Result<GameRulesetDto>.FieldFailure(nameof(request.Name), "Vui lòng nhập đầy đủ thông tin bắt buộc.");
        if (request.MaxStarters < request.MinStarters)
            return Result<GameRulesetDto>.FieldFailure(nameof(request.MaxStarters), "Số người chính thức tối đa không được nhỏ hơn tối thiểu.");
        if (!Formats.Contains(format))
            return Result<GameRulesetDto>.FieldFailure(nameof(request.DefaultFormat), "Thể thức mặc định không hợp lệ.");
        if (extensions.Length == 0)
            return Result<GameRulesetDto>.FieldFailure(nameof(request.AllowedEvidenceTypes),
                "Định dạng file phải là đuôi file cách nhau bằng dấu phẩy, ví dụ: png,jpg,mp4,rofl.");
        return null;
    }

    // "PNG, .jpg ,png" -> "png,jpg". null nếu có đuôi không hợp lệ hoặc danh sách rỗng.
    private static string? NormalizeExtensions(string raw)
    {
        var parts = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(p => p.TrimStart('.').ToLowerInvariant())
            .Distinct()
            .ToList();
        if (parts.Count == 0 || parts.Any(p => !ExtensionPattern.IsMatch(p))) return null;

        var joined = string.Join(",", parts);
        return joined.Length <= 200 ? joined : null;
    }

    private Task<bool> NameTakenAsync(int gameId, string name, int? exceptId)
    {
        var lower = name.ToLower();
        return _uow.Repository<GameRuleset>().AnyAsync(r =>
            r.GameId == gameId && r.Name.ToLower() == lower && (exceptId == null || r.GameRulesetId != exceptId));
    }

    private async Task<Dictionary<int, int>> OpenTournamentCountsAsync(int? rulesetId)
        => await _uow.Repository<TournamentRuleset>().Query()
            .Where(tr => (rulesetId == null || tr.GameRulesetId == rulesetId)
                && tr.Tournament.Status != "COMPLETED" && tr.Tournament.Status != "CANCELLED")
            .GroupBy(tr => tr.GameRulesetId)
            .Select(g => new { RulesetId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.RulesetId, x => x.Count);

    private static GameRulesetDto ToDto(GameRuleset r, int openCount) => new()
    {
        GameRulesetId = r.GameRulesetId,
        GameId = r.GameId,
        Name = r.Name,
        MinStarters = r.MinStarters,
        MaxStarters = r.MaxStarters,
        MaxSubstitutes = r.MaxSubstitutes,
        DefaultFormat = r.DefaultFormat,
        AllowedEvidenceTypes = r.AllowedEvidenceTypes,
        MaxEvidenceFileSizeMb = r.MaxEvidenceFileSizeMb,
        IsActive = r.Status == "ACTIVE",
        OpenTournamentCount = openCount
    };
}
