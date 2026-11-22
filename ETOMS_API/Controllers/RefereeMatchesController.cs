using ETOMS_API.Repositories;
using ETOMS_API.Services.Interface;
using ETOMS_API.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ETOMS_API.Controllers;

// Góp ý 22/11: trọng tài được gán xong không biết vào trận ở đâu. Danh sách trận người đang đăng nhập làm trọng tài
// (/referee/index.html) — mỗi trận dẫn tới trang trận, nơi trọng tài điểm danh, mở phòng, xử lý vi phạm, phán quyết tranh chấp.
[ApiController]
[Route("api/referee/matches")]
public sealed class RefereeMatchesController : ControllerBase
{
    private readonly EtomsDbContext _db;
    private readonly ICurrentUser _me;

    public RefereeMatchesController(EtomsDbContext db, ICurrentUser me)
    {
        _db = db;
        _me = me;
    }

    [HttpGet]
    public async Task<IActionResult> Mine(CancellationToken ct)
    {
        if (_me.UserId is not int id) return Unauthorized();
        var rows = await _db.Matches.AsNoTracking()
            .Where(m => m.RefereeAccountId == id && m.Tournament.Status != "CANCELLED")
            .OrderBy(m => m.Status == "VERIFIED" || m.Status == "WALKOVER" || m.Status == "CANCELLED")
            .ThenBy(m => m.ScheduledAt == null).ThenBy(m => m.ScheduledAt)
            .Take(100)
            .Select(m => new
            {
                m.MatchId, m.TournamentId, TournamentName = m.Tournament.Name, m.Status, m.ScheduledAt, m.BestOf, m.ScoreA, m.ScoreB,
                TeamA = m.TeamA != null ? m.TeamA.Name : null, TeamB = m.TeamB != null ? m.TeamB.Name : null,
                OpenDisputeId = m.Disputes.Where(d => d.Status == "OPEN" || d.Status == "UNDER_REVIEW").Select(d => (int?)d.DisputeId).FirstOrDefault()
            })
            .ToListAsync(ct);
        Response.Headers.CacheControl = "no-store";
        return Ok(rows.Select(m => new
        {
            m.MatchId, m.TournamentId, m.TournamentName, m.Status, StatusLabel = StatusText.Match(m.Status), StatusClass = StatusText.MatchClass(m.Status),
            m.ScheduledAt, m.BestOf, m.ScoreA, m.ScoreB, TeamA = m.TeamA ?? "Chưa xác định", TeamB = m.TeamB ?? "Chưa xác định", m.OpenDisputeId
        }));
    }
}
