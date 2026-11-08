using System.Text.RegularExpressions;
using ETOMS_API.DTOs.GameRule;
using ETOMS_API.Models;
using ETOMS_API.Repositories;
using ETOMS_API.Services.Interface;
using ETOMS_API.Utils;
using Microsoft.EntityFrameworkCore;

namespace ETOMS_API.Services.Impl;

// Module GRL-01. Thêm, sửa tên, ẩn game là ADM-03; ở đây chỉ sửa các cột luật của game.
// Luật này là nguồn kiểm tra khi tạo giải và khi đội đăng ký; giải đã tạo giữ số người của riêng nó.
public class GameRuleService : IGameRuleService
{
    private static readonly string[] Formats = { "SINGLE_ELIMINATION", "DOUBLE_ELIMINATION", "ROUND_ROBIN", "HYBRID" };
    private static readonly Regex ExtensionPattern = new("^[a-z0-9]{1,10}$", RegexOptions.Compiled);

    private readonly IUnitOfWork _uow;

    public GameRuleService(IUnitOfWork uow) => _uow = uow;

    public async Task<bool> IsAdminAsync(int accountId)
        => await _uow.Repository<Account>().AnyAsync(a => a.AccountId == accountId && a.Role == "ADMIN" && a.Status == "ACTIVE");

    public async Task<IReadOnlyList<GameRuleDto>> GetRulesAsync()
    {
        var openCounts = await OpenTournamentCountsAsync(null);
        var games = await _uow.Repository<Game>().Query().OrderBy(g => g.Title).ToListAsync();
        return games.Select(g => ToDto(g, openCounts.GetValueOrDefault(g.GameId))).ToList();
    }

    // ===== GRL-01: Sửa luật thi đấu của một tựa game =====
    public async Task<Result<GameRuleDto>> UpdateRuleAsync(int gameId, UpdateGameRuleRequest request)
    {
        var game = await _uow.Repository<Game>().GetByIdAsync(gameId);
        if (game is null) return Result<GameRuleDto>.Failure("Không tìm thấy tựa game.");

        if (request.MaxStarters < request.MinStarters)
            return Result<GameRuleDto>.FieldFailure(nameof(request.MaxStarters), "Số người chính thức tối đa không được nhỏ hơn tối thiểu.");

        var format = request.DefaultFormat.Trim().ToUpperInvariant();
        if (!Formats.Contains(format))
            return Result<GameRuleDto>.FieldFailure(nameof(request.DefaultFormat), "Thể thức mặc định không hợp lệ.");

        var extensions = NormalizeExtensions(request.AllowedEvidenceTypes);
        if (extensions is null)
            return Result<GameRuleDto>.FieldFailure(nameof(request.AllowedEvidenceTypes),
                "Định dạng file phải là đuôi file cách nhau bằng dấu phẩy, ví dụ: png,jpg,mp4,rofl.");

        game.MinStarters = request.MinStarters;
        game.MaxStarters = request.MaxStarters;
        game.MaxSubstitutes = request.MaxSubstitutes;
        game.DefaultFormat = format;
        game.AllowedEvidenceTypes = extensions;
        game.MaxEvidenceFileSizeMb = request.MaxEvidenceFileSizeMb;
        await _uow.SaveChangesAsync();

        var openCount = (await OpenTournamentCountsAsync(gameId)).GetValueOrDefault(gameId);
        var message = openCount > 0
            ? $"Đã lưu luật. Luật mới chỉ áp dụng cho giải tạo sau này; {openCount} giải chưa kết thúc vẫn giữ số người của giải."
            : "Đã lưu luật.";
        return Result<GameRuleDto>.Success(ToDto(game, openCount), message);
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

    private async Task<Dictionary<int, int>> OpenTournamentCountsAsync(int? gameId)
        => await _uow.Repository<Tournament>().Query()
            .Where(t => (gameId == null || t.GameId == gameId) && t.Status != "COMPLETED" && t.Status != "CANCELLED")
            .GroupBy(t => t.GameId)
            .Select(g => new { GameId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.GameId, x => x.Count);

    private static GameRuleDto ToDto(Game g, int openCount) => new()
    {
        GameId = g.GameId,
        Title = g.Title,
        ShortCode = g.ShortCode,
        IsActive = g.Status == "ACTIVE",
        MinStarters = g.MinStarters,
        MaxStarters = g.MaxStarters,
        MaxSubstitutes = g.MaxSubstitutes,
        DefaultFormat = g.DefaultFormat,
        AllowedEvidenceTypes = g.AllowedEvidenceTypes,
        MaxEvidenceFileSizeMb = g.MaxEvidenceFileSizeMb,
        OpenTournamentCount = openCount
    };
}
