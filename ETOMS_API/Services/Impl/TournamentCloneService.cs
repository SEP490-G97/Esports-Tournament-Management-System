using System.Data.Common;
using ETOMS_API.DTOs.Tournament;
using ETOMS_API.Models;
using ETOMS_API.Repositories;
using ETOMS_API.Repositories.Interface;
using ETOMS_API.Services.Interface;
using ETOMS_API.Utils;
using Microsoft.EntityFrameworkCore;

namespace ETOMS_API.Services.Impl;

// Module TOUR-14. Giải mới luôn ở DRAFT, thể lệ bắt đầu lại từ phiên bản 1. Đội, trận, tài trợ
// và link cộng tác viên thuộc riêng giải cũ nên không chép (BR-SPN-01).
public class TournamentCloneService : ITournamentCloneService
{
    private readonly IUnitOfWork _uow;

    public TournamentCloneService(IUnitOfWork uow) => _uow = uow;

    public async Task<int?> GetOrganizerAccountIdAsync(int tournamentId)
        => (await _uow.Repository<Tournament>().GetByIdAsync(tournamentId))?.OrganizerAccountId;

    // ===== TOUR-14: Nhân bản giải =====
    public async Task<Result<int>> CloneTournamentAsync(int sourceTournamentId, CloneTournamentRequest request)
    {
        var source = await _uow.Repository<Tournament>().GetByIdAsync(sourceTournamentId);
        if (source is null) return Result<int>.Failure("Không tìm thấy giải đấu.");

        var gameActive = await _uow.Repository<Game>().AnyAsync(g => g.GameId == source.GameId && g.Status == "ACTIVE");
        if (!gameActive) return Result<int>.Failure("Tựa game của giải này đã ngừng hoạt động, không thể nhân bản.");

        var name = request.Name.Trim();
        if (name.Length == 0)
            return Result<int>.FieldFailure(nameof(request.Name), "Vui lòng nhập đầy đủ thông tin bắt buộc.");

        var regOpen = request.RegOpenAt!.Value.ToUniversalTime();
        var regClose = request.RegCloseAt!.Value.ToUniversalTime();
        var start = request.StartAt!.Value.ToUniversalTime();
        if (regOpen >= regClose)
            return Result<int>.FieldFailure(nameof(request.RegCloseAt), "Thời gian đóng đăng ký phải sau thời gian mở đăng ký.");
        if (regClose >= start)
            return Result<int>.FieldFailure(nameof(request.RegCloseAt), "Thời gian đóng đăng ký phải trước ngày khai mạc.");
        if (regClose <= DateTime.UtcNow)
            return Result<int>.FieldFailure(nameof(request.RegCloseAt), "Thời gian đóng đăng ký phải ở tương lai.");

        var clone = CopyColumns(source);
        clone.TournamentId = 0;
        clone.Name = name;
        clone.RegOpenAt = regOpen;
        clone.RegCloseAt = regClose;
        clone.StartAt = start;
        clone.EndAt = null;
        clone.Status = "DRAFT";
        clone.RulebookVersion = 1;
        clone.StreamUrl = null;
        clone.CancelReason = null;
        clone.BracketLockedAt = null;
        clone.BracketLockedByAccountId = null;
        clone.CreatedAt = DateTime.UtcNow;

        await _uow.Repository<Tournament>().AddAsync(clone);
        await _uow.SaveChangesAsync();

        await CopyRulesetAsync(sourceTournamentId, clone.TournamentId);
        return Result<int>.Success(clone.TournamentId, "Đã nhân bản giải. Giải mới đang ở trạng thái nháp.");
    }

    // Bộ luật đã chọn (GRL-06) là một phần cấu hình. Bảng TournamentRuleset chưa chắc đã có trong
    // database chung; thiếu bảng thì bỏ qua để việc nhân bản giải vẫn thành công.
    private async Task CopyRulesetAsync(int sourceTournamentId, int cloneTournamentId)
    {
        try
        {
            var rulesetId = await _uow.Repository<TournamentRuleset>().Query()
                .Where(tr => tr.TournamentId == sourceTournamentId)
                .Select(tr => (int?)tr.GameRulesetId)
                .FirstOrDefaultAsync();
            if (rulesetId is null) return;

            await _uow.Repository<TournamentRuleset>().AddAsync(new TournamentRuleset
            {
                TournamentId = cloneTournamentId,
                GameRulesetId = rulesetId.Value,
                CreatedAt = DateTime.UtcNow
            });
            await _uow.SaveChangesAsync();
        }
        catch (Exception ex) when (ex is DbException or DbUpdateException)
        {
        }
    }

    // Chép mọi cột dữ liệu (bỏ qua navigation) để cột cấu hình thêm sau này tự được chép theo.
    private static Tournament CopyColumns(Tournament source)
    {
        var clone = new Tournament();
        foreach (var property in typeof(Tournament).GetProperties())
        {
            var isColumn = property.PropertyType.IsValueType || property.PropertyType == typeof(string);
            if (isColumn && property.CanWrite) property.SetValue(clone, property.GetValue(source));
        }
        return clone;
    }
}
