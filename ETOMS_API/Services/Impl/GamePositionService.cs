using System.Data.Common;
using ETOMS_API.DTOs.GameRule;
using ETOMS_API.Models;
using ETOMS_API.Repositories;
using ETOMS_API.Repositories.Interface;
using ETOMS_API.Services.Interface;
using ETOMS_API.Utils;
using Microsoft.EntityFrameworkCore;

namespace ETOMS_API.Services.Impl;

// Module GRL-03. Vị trí thuộc một bộ luật. Bảng GameRuleset và GamePosition chưa chắc đã có trong
// database chung, nên lỗi truy cập bảng được trả thành thông báo thay vì lỗi 500.
public class GamePositionService : IGamePositionService
{
    private const string TableMissingMessage = "Không truy cập được dữ liệu vị trí. Có thể bảng GameRuleset hoặc GamePosition chưa được tạo trong database.";

    private readonly IUnitOfWork _uow;

    public GamePositionService(IUnitOfWork uow) => _uow = uow;

    public async Task<Result<IReadOnlyList<GamePositionDto>>> GetPositionsAsync(int rulesetId)
    {
        try
        {
            if (!await RulesetExistsAsync(rulesetId)) return Result<IReadOnlyList<GamePositionDto>>.Failure("Không tìm thấy bộ luật.");
            return Result<IReadOnlyList<GamePositionDto>>.Success(await LoadAsync(rulesetId));
        }
        catch (DbException)
        {
            return Result<IReadOnlyList<GamePositionDto>>.Failure(TableMissingMessage);
        }
    }

    // ===== GRL-03: Thêm vị trí, xếp cuối danh sách =====
    public async Task<Result<GamePositionDto>> AddPositionAsync(int rulesetId, SaveGamePositionRequest request)
    {
        var name = request.Name.Trim();
        if (name.Length == 0)
            return Result<GamePositionDto>.FieldFailure(nameof(request.Name), "Vui lòng nhập đầy đủ thông tin bắt buộc.");

        try
        {
            if (!await RulesetExistsAsync(rulesetId)) return Result<GamePositionDto>.Failure("Không tìm thấy bộ luật.");
            if (await NameTakenAsync(rulesetId, name, null))
                return Result<GamePositionDto>.FieldFailure(nameof(request.Name), "Bộ luật này đã có vị trí cùng tên.");

            var maxOrder = await _uow.Repository<GamePosition>().Query()
                .Where(p => p.GameRulesetId == rulesetId)
                .MaxAsync(p => (int?)p.SortOrder) ?? 0;

            var position = new GamePosition { GameRulesetId = rulesetId, Name = name, SortOrder = maxOrder + 1, Status = "ACTIVE", CreatedAt = DateTime.UtcNow };
            await _uow.Repository<GamePosition>().AddAsync(position);
            await _uow.SaveChangesAsync();
            return Result<GamePositionDto>.Success(ToDto(position), "Đã thêm vị trí.");
        }
        catch (Exception ex) when (ex is DbException or DbUpdateException)
        {
            return Result<GamePositionDto>.Failure(TableMissingMessage);
        }
    }

    // ===== GRL-03: Đổi tên hoặc chuyển còn dùng / đã loại =====
    public async Task<Result<GamePositionDto>> UpdatePositionAsync(int rulesetId, int positionId, SaveGamePositionRequest request)
    {
        var name = request.Name.Trim();
        if (name.Length == 0)
            return Result<GamePositionDto>.FieldFailure(nameof(request.Name), "Vui lòng nhập đầy đủ thông tin bắt buộc.");

        var status = string.IsNullOrWhiteSpace(request.Status) ? null : request.Status.Trim().ToUpperInvariant();
        if (status is not null and not ("ACTIVE" or "RETIRED"))
            return Result<GamePositionDto>.Failure("Trạng thái vị trí không hợp lệ.");

        try
        {
            var position = await _uow.Repository<GamePosition>().Query()
                .FirstOrDefaultAsync(p => p.GamePositionId == positionId && p.GameRulesetId == rulesetId);
            if (position is null) return Result<GamePositionDto>.Failure("Không tìm thấy vị trí của bộ luật này.");

            if (await NameTakenAsync(rulesetId, name, positionId))
                return Result<GamePositionDto>.FieldFailure(nameof(request.Name), "Bộ luật này đã có vị trí cùng tên.");

            position.Name = name;
            if (status is not null) position.Status = status;
            await _uow.SaveChangesAsync();
            return Result<GamePositionDto>.Success(ToDto(position), "Đã lưu vị trí.");
        }
        catch (Exception ex) when (ex is DbException or DbUpdateException)
        {
            return Result<GamePositionDto>.Failure(TableMissingMessage);
        }
    }

    // ===== GRL-03: Đổi chỗ với vị trí liền trên hoặc liền dưới =====
    public async Task<Result<IReadOnlyList<GamePositionDto>>> MovePositionAsync(int rulesetId, int positionId, string direction)
    {
        var dir = direction.Trim().ToLowerInvariant();
        if (dir is not ("up" or "down")) return Result<IReadOnlyList<GamePositionDto>>.Failure("Hướng di chuyển không hợp lệ.");

        try
        {
            var all = await _uow.Repository<GamePosition>().Query()
                .Where(p => p.GameRulesetId == rulesetId)
                .OrderBy(p => p.SortOrder).ThenBy(p => p.GamePositionId)
                .ToListAsync();

            var index = all.FindIndex(p => p.GamePositionId == positionId);
            if (index < 0) return Result<IReadOnlyList<GamePositionDto>>.Failure("Không tìm thấy vị trí của bộ luật này.");

            var target = dir == "up" ? index - 1 : index + 1;
            if (target < 0 || target >= all.Count)
                return Result<IReadOnlyList<GamePositionDto>>.Success(all.Select(ToDto).ToList(), "Vị trí đã ở đầu hoặc cuối danh sách.");

            (all[index], all[target]) = (all[target], all[index]);
            // Đánh số lại 1..n để thứ tự luôn liền mạch, kể cả khi dữ liệu cũ bị trùng số.
            for (var i = 0; i < all.Count; i++) all[i].SortOrder = i + 1;
            await _uow.SaveChangesAsync();
            return Result<IReadOnlyList<GamePositionDto>>.Success(all.Select(ToDto).ToList(), "Đã đổi thứ tự.");
        }
        catch (Exception ex) when (ex is DbException or DbUpdateException)
        {
            return Result<IReadOnlyList<GamePositionDto>>.Failure(TableMissingMessage);
        }
    }

    private Task<bool> RulesetExistsAsync(int rulesetId)
        => _uow.Repository<GameRuleset>().AnyAsync(r => r.GameRulesetId == rulesetId);

    private async Task<IReadOnlyList<GamePositionDto>> LoadAsync(int rulesetId)
        => (await _uow.Repository<GamePosition>().Query()
                .Where(p => p.GameRulesetId == rulesetId)
                .OrderBy(p => p.SortOrder).ThenBy(p => p.GamePositionId)
                .ToListAsync())
            .Select(ToDto).ToList();

    private Task<bool> NameTakenAsync(int rulesetId, string name, int? exceptId)
    {
        var lower = name.ToLower();
        return _uow.Repository<GamePosition>().AnyAsync(p =>
            p.GameRulesetId == rulesetId && p.Name.ToLower() == lower && (exceptId == null || p.GamePositionId != exceptId));
    }

    private static GamePositionDto ToDto(GamePosition p) => new()
    {
        GamePositionId = p.GamePositionId,
        GameRulesetId = p.GameRulesetId,
        Name = p.Name,
        SortOrder = p.SortOrder,
        Status = p.Status,
        IsActive = p.Status == "ACTIVE"
    };
}
