using ETOMS_API.DTOs.TournamentSetup;
using ETOMS_API.Services.Interface;
using Microsoft.AspNetCore.Mvc;

namespace ETOMS_API.Controllers
{
    // STUB TẠM sửa lại phần kiểm quyền thay module TOUR (Nguyễn Quang Anh) — bản trước
    // GetCurrentAccountId() đọc sai claim (luôn ra 0) và [Authorize(Roles=...)] bị tắt nên
    // Player vẫn tạo/sửa được giải đấu của người khác. Không phải bài làm cá nhân của bạn ấy.
    [ApiController]
    [Route("api/[controller]")]
    public class TournamentSetupController : ControllerBase
    {
        private readonly ITournamentSetupService _setupService;
        private readonly ICurrentUser _currentUser;

        public TournamentSetupController(ITournamentSetupService setupService, ICurrentUser currentUser)
        {
            _setupService = setupService;
            _currentUser = currentUser;
        }

        // TOUR-01: Tạo giải - Step 1 Thông tin chung — chỉ ORGANIZER/ADMIN.
        [HttpPost("step1")]
        public async Task<IActionResult> CreateStep1([FromBody] Step1GeneralInfoDto dto)
        {
            if (!_currentUser.IsAuthenticated) return Unauthorized();
            if (!await _setupService.IsOrganizerOrAdminAsync(_currentUser.UserId!.Value))
                return StatusCode(403, new { Message = "Chỉ Ban tổ chức/Admin được tạo giải đấu." });
            try
            {
                int tournamentId = await _setupService.CreateStep1GeneralInfoAsync(_currentUser.UserId!.Value, dto);
                return Ok(new { Message = "Khởi tạo giải đấu thành công", TournamentId = tournamentId });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
        }

        // TOUR-02: Step 2 Timeline & Quy mô — chỉ người đã tạo giải hoặc Admin.
        [HttpPut("{id}/step2-timeline")]
        public async Task<IActionResult> UpdateStep2(int id, [FromBody] Step2TimelineDto dto)
        {
            var denied = await CheckCanManageAsync(id);
            if (denied is not null) return denied;
            try
            {
                await _setupService.UpdateStep2TimelineAsync(id, dto);
                return Ok(new { Message = "Cập nhật timeline và quy mô thành công." });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = ex.Message });
            }
        }

        // TOUR-05: Step 3 BO Config — chỉ người đã tạo giải hoặc Admin.
        [HttpPut("{id}/step3-bo-config")]
        public async Task<IActionResult> UpdateStep3(int id, [FromBody] Step3BoConfigDto dto)
        {
            var denied = await CheckCanManageAsync(id);
            if (denied is not null) return denied;
            try
            {
                await _setupService.UpdateStep3BoConfigAsync(id, dto);
                return Ok(new { Message = "Cập nhật cấu hình Best-of thành công." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
        }

        // TOUR-06: Step 4 Finance — chỉ người đã tạo giải hoặc Admin.
        [HttpPut("{id}/step4-finance")]
        public async Task<IActionResult> UpdateStep4(int id, [FromBody] Step4FinanceDto dto)
        {
            var denied = await CheckCanManageAsync(id);
            if (denied is not null) return denied;
            try
            {
                await _setupService.UpdateStep4FinanceAsync(id, dto);
                return Ok(new { Message = "Cập nhật thông tin lệ phí và giải thưởng thành công." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
        }

        // TOUR-07: Step 5 Checklist & Publish — chỉ người đã tạo giải hoặc Admin.
        [HttpPost("{id}/publish")]
        public async Task<IActionResult> PublishTournament(int id, [FromBody] Step5RequirementDto dto)
        {
            var denied = await CheckCanManageAsync(id);
            if (denied is not null) return denied;
            try
            {
                await _setupService.FinalizeStep5PublishAsync(id, dto);
                return Ok(new { Message = "Công bố giải đấu thành công." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
        }

        private async Task<IActionResult?> CheckCanManageAsync(int tournamentId)
        {
            if (!_currentUser.IsAuthenticated) return Unauthorized();
            var canManage = await _setupService.CanManageAsync(tournamentId, _currentUser.UserId!.Value);
            if (canManage is null) return NotFound(new { Message = "Không tìm thấy giải đấu." });
            if (canManage != true) return StatusCode(403, new { Message = "Chỉ Ban tổ chức của giải này hoặc Admin được thao tác." });
            return null;
        }
    }
}