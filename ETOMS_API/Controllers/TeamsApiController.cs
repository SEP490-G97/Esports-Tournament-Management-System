using ETOMS_API.DTOs.Team;
using ETOMS_API.Services;
using ETOMS_API.Utils;
using Microsoft.AspNetCore.Mvc;

namespace ETOMS_API.Controllers;

// API JSON cho module TEAM. Tiến độ Iteration 1 — mới có TEAM-01 (Khởi tạo đội) và
// TEAM-02 (Trang quản trị đội). Các chức năng còn lại (Roster, Mã mời, Gia nhập,
// Rời đội, Chuyển quyền, Giải tán...) sẽ bổ sung ở các commit tiếp theo.
// Đăng nhập vẫn qua session (ICurrentUser đọc từ /DevAuth — stub tạm thay AUTH thật).
[Route("api/teams")]
public class TeamsApiController : ApiControllerBase
{
    private readonly ITeamService _teamService;
    private readonly ICurrentUser _currentUser;
    private readonly IWebHostEnvironment _env;

    public TeamsApiController(ITeamService teamService, ICurrentUser currentUser, IWebHostEnvironment env)
    {
        _teamService = teamService;
        _currentUser = currentUser;
        _env = env;
    }

    // GET /api/teams/games — danh sách tựa game đang mở (cho dropdown khi tạo đội).
    [HttpGet("games")]
    public async Task<IActionResult> GetGames()
        => Ok(await _teamService.GetActiveGamesAsync());

    // GET /api/teams/{id}/dashboard — TEAM-02, chỉ đội trưởng.
    [HttpGet("{id:int}/dashboard")]
    public async Task<IActionResult> GetDashboard(int id)
    {
        if (!_currentUser.IsAuthenticated) return Unauthorized();
        var dash = await _teamService.GetDashboardAsync(id);
        if (dash is null) return NotFound();
        if (dash.CaptainAccountId != _currentUser.UserId!.Value) return StatusCode(403);
        return Ok(dash);
    }

    // POST /api/teams — TEAM-01 (multipart/form-data: Name, Tag, GameId, logoFile tùy chọn).
    [HttpPost]
    public async Task<IActionResult> Create([FromForm] CreateTeamRequest model, IFormFile? logoFile)
    {
        if (!_currentUser.IsAuthenticated) return Unauthorized();
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var upload = ImageUploader.Save(logoFile, _env, "teams");
        if (!upload.Ok) return BadRequest(new { message = upload.Error, fieldErrors = new Dictionary<string, string> { ["logoFile"] = upload.Error! } });
        model.LogoUrl = upload.Url;

        var result = await _teamService.CreateTeamAsync(model, _currentUser.UserId!.Value);
        if (!result.IsSuccess) return FromResult(result);
        return CreatedAtAction(nameof(GetDashboard), new { id = result.Data }, new { message = result.Message, data = result.Data });
    }
}
