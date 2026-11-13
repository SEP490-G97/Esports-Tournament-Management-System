using System.Security.Claims;
using ETOMS_API.DTOs.TournamentSetup;
using ETOMS_API.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ETOMS_API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    //[Authorize(Roles = "ORGANIZER,ADMIN")]
    public class TournamentSetupController : ControllerBase
    {
        private readonly ITournamentSetupService _setupService;

        public TournamentSetupController(ITournamentSetupService setupService)
        {
            _setupService = setupService;
        }

        private int GetCurrentAccountId()
        {
            return int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        }

        // TOUR-01: Tạo giải - Step 1 Thông tin chung
        [HttpPost("step1")]
        public async Task<IActionResult> CreateStep1([FromBody] Step1GeneralInfoDto dto)
        {
            try
            {
                int tournamentId = await _setupService.CreateStep1GeneralInfoAsync(GetCurrentAccountId(), dto);
                return Ok(new { Message = "Khởi tạo giải đấu thành công", TournamentId = tournamentId });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
        }

        // TOUR-02: Step 2 Timeline & Quy mô
        [HttpPut("{id}/step2-timeline")]
        public async Task<IActionResult> UpdateStep2(int id, [FromBody] Step2TimelineDto dto)
        {
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

        // TOUR-05: Step 3 BO Config
        [HttpPut("{id}/step3-bo-config")]
        public async Task<IActionResult> UpdateStep3(int id, [FromBody] Step3BoConfigDto dto)
        {
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

        // TOUR-06: Step 4 Finance
        [HttpPut("{id}/step4-finance")]
        public async Task<IActionResult> UpdateStep4(int id, [FromBody] Step4FinanceDto dto)
        {
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

        // TOUR-07: Step 5 Checklist & Publish
        [HttpPost("{id}/publish")]
        public async Task<IActionResult> PublishTournament(int id, [FromBody] Step5RequirementDto dto)
        {
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
    }
}