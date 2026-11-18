using ETOMS_API.DTOs.Tournament;
using ETOMS_API.Services.Interface;
using Microsoft.AspNetCore.Mvc;

namespace ETOMS_API.Controllers;

// POR-02 / UC-36: danh sách & tìm kiếm giải — công khai, khách không cần tài khoản.
[Route("api/tournaments")]
public sealed class TournamentDirectoryController : ApiControllerBase
{
    private readonly ITournamentDirectoryService _directory;
    private readonly ICurrentUser _currentUser;

    public TournamentDirectoryController(ITournamentDirectoryService directory, ICurrentUser currentUser)
    {
        _directory = directory;
        _currentUser = currentUser;
    }

    // GET /api/tournaments/{id}/overview — POR-03, công khai (giải Nháp chỉ BTC của giải xem được).
    [HttpGet("{tournamentId:int}/overview")]
    public async Task<IActionResult> Overview(int tournamentId, CancellationToken ct)
        => FromResult(await _directory.GetOverviewAsync(tournamentId, _currentUser.UserId, ct));

    // GET /api/tournaments/{id}/participants — POR-04, công khai.
    [HttpGet("{tournamentId:int}/participants")]
    public async Task<IActionResult> Participants(int tournamentId, CancellationToken ct)
        => FromResult(await _directory.GetParticipantsAsync(tournamentId, _currentUser.UserId, ct));

    // GET /api/tournaments?q=&gameId=&state=&from=&to=&region=&fee=&sort=&page=&pageSize=
    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] TournamentDirectoryQuery query, CancellationToken ct)
        => FromResult(await _directory.SearchAsync(query, ct));

    // GET /api/tournaments/filters — tựa game, khu vực để đổ vào ô lọc.
    [HttpGet("filters")]
    public async Task<IActionResult> Filters(CancellationToken ct)
        => Ok(new { message = string.Empty, data = await _directory.GetFiltersAsync(ct) });
}
