using ETOMS_API.Models;
using ETOMS_API.Repositories;
using ETOMS_API.Services.Interface;
using Microsoft.EntityFrameworkCore;

namespace ETOMS_API.Services.Impl;

/// <summary>Sau khi trận có kết quả cuối (vd thắng trắng): nếu giải là Double Elimination thì đưa đội đi tiếp trong nhánh (BRK-05).</summary>
public class DoubleEliminationResultListener : IMatchResultListener
{
    private readonly EtomsDbContext _db;
    private readonly IDoubleEliminationService _de;

    public DoubleEliminationResultListener(EtomsDbContext db, IDoubleEliminationService de)
    {
        _db = db;
        _de = de;
    }

    public async Task OnResultAsync(int matchId, CancellationToken ct)
    {
        var format = await _db.Set<Match>().AsNoTracking().Where(m => m.MatchId == matchId)
            .Join(_db.Set<Tournament>().AsNoTracking(), m => m.TournamentId, t => t.TournamentId, (m, t) => t.Format)
            .FirstOrDefaultAsync(ct);
        if (format != "DOUBLE_ELIMINATION") return;

        var r = await _de.AdvanceAsync(matchId, ct);
        if (r.Error != BracketError.None)
            throw new InvalidOperationException($"Không đưa đội đi tiếp được cho trận {matchId}: {r.Error}");
    }
}