using ETOMS_API.Models;
using ETOMS_API.Repositories;
using ETOMS_API.Services.Interface;
using Microsoft.EntityFrameworkCore;

namespace ETOMS_API.Services.Impl;

/// <summary>
/// Sau khi trận có kết quả cuối từ luồng điểm danh / No-show (MAT, code của Hải): đưa đội đi tiếp trong nhánh bằng
/// ĐÚNG hàm mà nhập kết quả và xử thắng trắng đang dùng (IResultService.AdvanceWinnerAsync — loại trực tiếp và loại kép
/// theo cây do BracketService sinh). Gỡ cờ No-show thì hoàn tác các ô đã điền; trận kế tiếp đã đấu thì chặn.
/// (Thay bộ Loại kép riêng của nhánh main — DoubleEliminationService/Engine/Planner, đọc cây theo cấu trúc khác
/// GroupLabel WB/LB/GF nên không khớp cây do BracketService sinh; đã bỏ 18/11 vì không còn nơi nào gọi.)
/// </summary>
public sealed class BracketResultListener : IMatchResultListener
{
    private readonly EtomsDbContext _db;
    private readonly IResultService _results;

    public BracketResultListener(EtomsDbContext db, IResultService results)
    {
        _db = db;
        _results = results;
    }

    public async Task OnResultAsync(int matchId, CancellationToken ct)
    {
        var winner = await _db.Set<Match>().AsNoTracking().Where(m => m.MatchId == matchId)
            .Select(m => m.WinnerTeamId).FirstOrDefaultAsync(ct);
        if (winner is int teamId) await _results.AdvanceWinnerAsync(matchId, teamId);
    }

    public Task OnResultRevertAsync(int matchId, CancellationToken ct) => _results.RevertAdvanceAsync(matchId);
}
