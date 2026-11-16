using ETOMS_API.DTOs.Team;
using ETOMS_API.Services.Interface;
using ETOMS_API.Utils;
using Microsoft.AspNetCore.Mvc;

namespace ETOMS_API.Controllers;

// API JSON cho module TEAM — song song với TeamController (MVC, trả HTML), dùng chung
// ITeamService + DTO. Người dùng hiện tại lấy từ JWT qua ICurrentUser.
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

    // GET /api/teams?q=&gameId=&status=&sort=&page=&pageSize= — trả { items, total, page, pageSize, totalPages }.
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] TeamSearchRequest filter)
        => FromResult(await _teamService.SearchTeamsAsync(filter));

    // GET /api/teams/games — danh sách tựa game đang mở (cho dropdown).
    [HttpGet("games")]
    public async Task<IActionResult> GetGames()
        => Ok(await _teamService.GetActiveGamesAsync());

    // GET /api/teams/mine — đội của tài khoản đang đăng nhập (null nếu chưa ở đội nào). Cho widget nav.
    [HttpGet("mine")]
    public async Task<IActionResult> GetMine()
    {
        if (!_currentUser.IsAuthenticated) return Unauthorized();
        return Ok(await _teamService.GetMyTeamAsync(_currentUser.UserId!.Value));
    }

    // GET /api/teams/my-teams — TẤT CẢ đội của tài khoản đang đăng nhập (mỗi tựa game tối đa một đội).
    // Cho trang "Các đội của tôi" (/team/mine.html) và ô chọn đội khi đăng tin tuyển thành viên.
    [HttpGet("my-teams")]
    public async Task<IActionResult> GetMyTeams()
    {
        if (!_currentUser.IsAuthenticated) return Unauthorized();
        return Ok(await _teamService.GetMyTeamsAsync(_currentUser.UserId!.Value));
    }

    // GET /api/teams/player-search?q=&gameId= — TEAM-01: tìm tuyển thủ để mời khi tạo đội (cần đăng nhập).
    [HttpGet("player-search")]
    public async Task<IActionResult> SearchPlayers([FromQuery] string? q, [FromQuery] int gameId)
    {
        if (!_currentUser.IsAuthenticated) return Unauthorized();
        return FromResult(await _teamService.SearchPlayersAsync(q, gameId, _currentUser.UserId!.Value));
    }

    // GET /api/teams/invitations — lời mời vào đội đang chờ tài khoản này trả lời.
    [HttpGet("invitations")]
    public async Task<IActionResult> GetMyInvitations()
    {
        if (!_currentUser.IsAuthenticated) return Unauthorized();
        return Ok(await _teamService.GetMyInvitationsAsync(_currentUser.UserId!.Value));
    }

    // POST /api/teams/invitations/{teamMemberId}/accept | /decline — chỉ chính người được mời.
    [HttpPost("invitations/{teamMemberId:int}/accept")]
    public async Task<IActionResult> AcceptInvitation(int teamMemberId)
    {
        if (!_currentUser.IsAuthenticated) return Unauthorized();
        return FromResult(await _teamService.RespondInvitationAsync(teamMemberId, _currentUser.UserId!.Value, true));
    }

    [HttpPost("invitations/{teamMemberId:int}/decline")]
    public async Task<IActionResult> DeclineInvitation(int teamMemberId)
    {
        if (!_currentUser.IsAuthenticated) return Unauthorized();
        return FromResult(await _teamService.RespondInvitationAsync(teamMemberId, _currentUser.UserId!.Value, false));
    }

    // GET /api/teams/{id}
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetDetail(int id)
    {
        var team = await _teamService.GetTeamDetailAsync(id);
        return team is null ? NotFound() : Ok(team);
    }

    // GET /api/teams/{id}/upcoming-matches — trận chưa kết thúc của đội (công khai: chỉ đối thủ, giờ, trạng thái).
    [HttpGet("{id:int}/upcoming-matches")]
    public async Task<IActionResult> GetUpcomingMatches(int id)
        => Ok(new { message = string.Empty, data = await _teamService.GetUpcomingMatchesAsync(id) });

    // GET /api/teams/{id}/roster — TEAM-03, chỉ đội trưởng.
    [HttpGet("{id:int}/roster")]
    public async Task<IActionResult> GetRoster(int id)
    {
        if (!_currentUser.IsAuthenticated) return Unauthorized();
        var captainId = await _teamService.GetCaptainAccountIdAsync(id);
        if (captainId is null) return NotFound();
        if (captainId != _currentUser.UserId!.Value) return StatusCode(403);
        return Ok(await _teamService.GetRosterAsync(id));
    }

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

    // GET /api/teams/{id}/invite — TEAM-04, chỉ đội trưởng.
    [HttpGet("{id:int}/invite")]
    public async Task<IActionResult> GetInvite(int id)
    {
        if (!_currentUser.IsAuthenticated) return Unauthorized();
        var captainId = await _teamService.GetCaptainAccountIdAsync(id);
        if (captainId is null) return NotFound();
        if (captainId != _currentUser.UserId!.Value) return StatusCode(403);
        return Ok(await _teamService.GetInviteAsync(id));
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
        if (!result.IsSuccess)
        {
            ImageUploader.Delete(model.LogoUrl, _env);   // tạo đội thất bại thì không giữ logo mồ côi
            return FromResult(result);
        }
        return CreatedAtAction(nameof(GetDetail), new { id = result.Data }, new { message = result.Message, data = result.Data });
    }

    // POST /api/teams/join — TEAM-05.
    [HttpPost("join")]
    public async Task<IActionResult> Join([FromBody] JoinTeamRequest model)
    {
        if (!_currentUser.IsAuthenticated) return Unauthorized();
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        return FromResult(await _teamService.JoinByCodeAsync(model.Code, _currentUser.UserId!.Value));
    }

    // POST /api/teams/{id}/invite — TEAM-04, tạo mã mời mới (thay mã cũ).
    [HttpPost("{id:int}/invite")]
    public async Task<IActionResult> GenerateInvite(int id, [FromBody] CreateInviteRequest model)
    {
        if (!_currentUser.IsAuthenticated) return Unauthorized();
        var captainId = await _teamService.GetCaptainAccountIdAsync(id);
        if (captainId is null) return NotFound();
        if (captainId != _currentUser.UserId!.Value) return StatusCode(403);
        return FromResult(await _teamService.GenerateInviteAsync(id, _currentUser.UserId!.Value, model));
    }

    // DELETE /api/teams/{id}/invite — thu hồi mã mời hiện tại.
    [HttpDelete("{id:int}/invite")]
    public async Task<IActionResult> RevokeInvite(int id)
    {
        if (!_currentUser.IsAuthenticated) return Unauthorized();
        return FromResult(await _teamService.RevokeInviteAsync(id, _currentUser.UserId!.Value));
    }

    // POST /api/teams/{id}/members/{targetAccountId}/slot — đổi chính thức/dự bị.
    [HttpPost("{id:int}/members/{targetAccountId:int}/slot")]
    public async Task<IActionResult> ChangeSlot(int id, int targetAccountId, [FromBody] ChangeSlotRequest body)
    {
        if (!_currentUser.IsAuthenticated) return Unauthorized();
        return FromResult(await _teamService.ChangeSlotAsync(id, targetAccountId, body.NewSlot, _currentUser.UserId!.Value));
    }

    // POST /api/teams/{id}/members/{targetAccountId}/approve — duyệt đơn ứng tuyển/lời mời.
    [HttpPost("{id:int}/members/{targetAccountId:int}/approve")]
    public async Task<IActionResult> ApproveMember(int id, int targetAccountId)
    {
        if (!_currentUser.IsAuthenticated) return Unauthorized();
        return FromResult(await _teamService.ApproveMemberAsync(id, targetAccountId, _currentUser.UserId!.Value));
    }

    // POST /api/teams/{id}/members/{targetAccountId}/reject — từ chối đơn ứng tuyển/lời mời.
    [HttpPost("{id:int}/members/{targetAccountId:int}/reject")]
    public async Task<IActionResult> RejectMember(int id, int targetAccountId)
    {
        if (!_currentUser.IsAuthenticated) return Unauthorized();
        return FromResult(await _teamService.RejectMemberAsync(id, targetAccountId, _currentUser.UserId!.Value));
    }

    // DELETE /api/teams/{id}/members/{targetAccountId} — loại thành viên.
    [HttpDelete("{id:int}/members/{targetAccountId:int}")]
    public async Task<IActionResult> KickMember(int id, int targetAccountId)
    {
        if (!_currentUser.IsAuthenticated) return Unauthorized();
        return FromResult(await _teamService.KickMemberAsync(id, targetAccountId, _currentUser.UserId!.Value));
    }

    // POST /api/teams/{id}/captain — TEAM-07, chuyển quyền đội trưởng.
    [HttpPost("{id:int}/captain")]
    public async Task<IActionResult> TransferCaptain(int id, [FromBody] TransferCaptainRequest body)
    {
        if (!_currentUser.IsAuthenticated) return Unauthorized();
        return FromResult(await _teamService.TransferCaptainAsync(id, body.NewCaptainAccountId, _currentUser.UserId!.Value));
    }

    // POST /api/teams/{id}/leave — TEAM-06.
    [HttpPost("{id:int}/leave")]
    public async Task<IActionResult> Leave(int id)
    {
        if (!_currentUser.IsAuthenticated) return Unauthorized();
        return FromResult(await _teamService.LeaveTeamAsync(id, _currentUser.UserId!.Value));
    }

    // DELETE /api/teams/{id} — TEAM-08, giải tán đội (xóa mềm).
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Disband(int id)
    {
        if (!_currentUser.IsAuthenticated) return Unauthorized();
        return FromResult(await _teamService.DisbandTeamAsync(id, _currentUser.UserId!.Value));
    }
}

// Body nhỏ cho 2 action không có DTO sẵn ở tầng Application (vì MVC truyền thẳng qua route/form).
public class ChangeSlotRequest { public string NewSlot { get; set; } = string.Empty; }
public class TransferCaptainRequest { public int NewCaptainAccountId { get; set; } }



