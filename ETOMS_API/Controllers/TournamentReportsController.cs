using ETOMS_API.Services.Interface;
using ETOMS_API.Utils;
using Microsoft.AspNetCore.Mvc;

namespace ETOMS_API.Controllers;

[Route("api/reports/tournaments")]
public sealed class TournamentReportsController : ApiControllerBase
{
    private readonly IRankingService _rankingService;
    private readonly ICurrentUser _currentUser;

    public TournamentReportsController(IRankingService rankingService, ICurrentUser currentUser)
    {
        _rankingService = rankingService;
        _currentUser = currentUser;
    }

    [HttpGet("{tournamentId:int}")]
    public async Task<IActionResult> GetReport(int tournamentId, CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not int accountId) return Unauthorized();
        return FromResult(await _rankingService.GetTournamentReportAsync(
            tournamentId, accountId, cancellationToken));
    }

    [HttpGet("{tournamentId:int}/excel")]
    public async Task<IActionResult> ExportExcel(int tournamentId, CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not int accountId) return Unauthorized();
        var result = await _rankingService.GetTournamentReportAsync(
            tournamentId, accountId, cancellationToken);
        if (!result.IsSuccess) return FromResult(result);

        var file = SpreadsheetMlExporter.Build(result.Data!);
        return File(file, "application/vnd.ms-excel; charset=utf-8",
            $"ETOMS-giai-{tournamentId}-bao-cao.xls");
    }
}
