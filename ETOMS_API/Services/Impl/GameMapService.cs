using System.Data.Common;
using ETOMS_API.DTOs.GameRule;
using ETOMS_API.Models;
using ETOMS_API.Repositories;
using ETOMS_API.Services.Interface;
using ETOMS_API.Utils;
using Microsoft.EntityFrameworkCore;

namespace ETOMS_API.Services.Impl;

// Module GRL-02. Bản đồ thuộc một bộ luật. Bảng GameRuleset và GameMap chưa chắc đã có trong
// database chung, nên lỗi truy cập bảng được trả thành thông báo thay vì lỗi 500.
public class GameMapService : IGameMapService
{
    private const string TableMissingMessage = "Không truy cập được dữ liệu bản đồ. Có thể bảng GameRuleset hoặc GameMap chưa được tạo trong database.";

    private readonly IUnitOfWork _uow;

    public GameMapService(IUnitOfWork uow) => _uow = uow;

    public async Task<Result<IReadOnlyList<GameMapDto>>> GetMapsAsync(int rulesetId)
    {
        try
        {
            if (!await RulesetExistsAsync(rulesetId))
                return Result<IReadOnlyList<GameMapDto>>.Failure("Không tìm thấy bộ luật.");

            var maps = await _uow.Repository<GameMap>().Query()
                .Where(m => m.GameRulesetId == rulesetId)
                .OrderBy(m => m.Status == "ACTIVE" ? 0 : 1).ThenBy(m => m.Name)
                .ToListAsync();
            return Result<IReadOnlyList<GameMapDto>>.Success(maps.Select(ToDto).ToList());
        }
        catch (DbException)
        {
            return Result<IReadOnlyList<GameMapDto>>.Failure(TableMissingMessage);
        }
    }

    // ===== GRL-02: Thêm bản đồ =====
    public async Task<Result<GameMapDto>> AddMapAsync(int rulesetId, SaveGameMapRequest request, string? imageUrl)
    {
        var name = request.Name.Trim();
        if (name.Length == 0)
            return Result<GameMapDto>.FieldFailure(nameof(request.Name), "Vui lòng nhập đầy đủ thông tin bắt buộc.");

        try
        {
            if (!await RulesetExistsAsync(rulesetId)) return Result<GameMapDto>.Failure("Không tìm thấy bộ luật.");
            if (await NameTakenAsync(rulesetId, name, null))
                return Result<GameMapDto>.FieldFailure(nameof(request.Name), "Bộ luật này đã có bản đồ cùng tên.");

            var map = new GameMap { GameRulesetId = rulesetId, Name = name, ImageUrl = imageUrl, Status = "ACTIVE", CreatedAt = DateTime.UtcNow };
            await _uow.Repository<GameMap>().AddAsync(map);
            await _uow.SaveChangesAsync();
            return Result<GameMapDto>.Success(ToDto(map), "Đã thêm bản đồ.");
        }
        catch (Exception ex) when (ex is DbException or DbUpdateException)
        {
            return Result<GameMapDto>.Failure(TableMissingMessage);
        }
    }

    // ===== GRL-02: Sửa tên, ảnh hoặc chuyển còn dùng / đã loại =====
    public async Task<Result<GameMapDto>> UpdateMapAsync(int rulesetId, int mapId, SaveGameMapRequest request, string? newImageUrl)
    {
        var name = request.Name.Trim();
        if (name.Length == 0)
            return Result<GameMapDto>.FieldFailure(nameof(request.Name), "Vui lòng nhập đầy đủ thông tin bắt buộc.");

        var status = string.IsNullOrWhiteSpace(request.Status) ? null : request.Status.Trim().ToUpperInvariant();
        if (status is not null and not ("ACTIVE" or "RETIRED"))
            return Result<GameMapDto>.Failure("Trạng thái bản đồ không hợp lệ.");

        try
        {
            var map = await _uow.Repository<GameMap>().Query()
                .FirstOrDefaultAsync(m => m.GameMapId == mapId && m.GameRulesetId == rulesetId);
            if (map is null) return Result<GameMapDto>.Failure("Không tìm thấy bản đồ của bộ luật này.");

            if (await NameTakenAsync(rulesetId, name, mapId))
                return Result<GameMapDto>.FieldFailure(nameof(request.Name), "Bộ luật này đã có bản đồ cùng tên.");

            map.Name = name;
            if (newImageUrl is not null) map.ImageUrl = newImageUrl;
            if (status is not null) map.Status = status;
            await _uow.SaveChangesAsync();
            return Result<GameMapDto>.Success(ToDto(map), "Đã lưu bản đồ.");
        }
        catch (Exception ex) when (ex is DbException or DbUpdateException)
        {
            return Result<GameMapDto>.Failure(TableMissingMessage);
        }
    }

    private Task<bool> RulesetExistsAsync(int rulesetId)
        => _uow.Repository<GameRuleset>().AnyAsync(r => r.GameRulesetId == rulesetId);

    private Task<bool> NameTakenAsync(int rulesetId, string name, int? exceptMapId)
    {
        var lower = name.ToLower();
        return _uow.Repository<GameMap>().AnyAsync(m =>
            m.GameRulesetId == rulesetId && m.Name.ToLower() == lower && (exceptMapId == null || m.GameMapId != exceptMapId));
    }

    private static GameMapDto ToDto(GameMap m) => new()
    {
        GameMapId = m.GameMapId,
        GameRulesetId = m.GameRulesetId,
        Name = m.Name,
        ImageUrl = m.ImageUrl,
        Status = m.Status,
        IsActive = m.Status == "ACTIVE"
    };
}
