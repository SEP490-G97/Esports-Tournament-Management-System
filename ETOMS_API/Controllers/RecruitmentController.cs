using System.Security.Claims;
using ETOMS_API.DTOs.Recruitment;
using ETOMS_API.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ETOMS_API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    //[Authorize]
    public class RecruitmentController : ControllerBase
    {
        private readonly IRecruitmentService _recruitmentService;

        public RecruitmentController(IRecruitmentService recruitmentService)
        {
            _recruitmentService = recruitmentService;
        }

        private int GetCurrentAccountId()
        {
            return int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        }

        // FA-01: Đăng tin tuyển thành viên
        [HttpPost("lfp")]
        public async Task<IActionResult> CreateLFP([FromBody] CreateLFPPostDto dto)
        {
            try
            {
                int postId = await _recruitmentService.CreateLFPPostAsync(GetCurrentAccountId(), dto);
                return CreatedAtAction(nameof(CreateLFP), new { id = postId }, new { Message = "Đăng tin LFP thành công", PostId = postId });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
        }

        // FA-02: Đăng tin tìm đội
        [HttpPost("lft")]
        public async Task<IActionResult> CreateLFT([FromBody] CreateLFTPostDto dto)
        {
            try
            {
                int postId = await _recruitmentService.CreateLFTPostAsync(GetCurrentAccountId(), dto);
                return CreatedAtAction(nameof(CreateLFT), new { id = postId }, new { Message = "Đăng tin LFT thành công", PostId = postId });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
        }

        // FA-03: Bảng tin Tuyển quân (Công khai)
        [HttpGet("board")]
        [AllowAnonymous]
        public async Task<IActionResult> GetBoard([FromQuery] RecruitmentBoardQueryDto query)
        {
            var result = await _recruitmentService.GetRecruitmentBoardAsync(query);
            return Ok(result);
        }

        // FA-04: Nộp đơn ứng tuyển
        [HttpPost("apply")]
        public async Task<IActionResult> Apply([FromBody] ApplyLFPPostDto dto)
        {
            try
            {
                await _recruitmentService.ApplyToLFPAsync(GetCurrentAccountId(), dto);
                return Ok(new { Message = "Gửi đơn ứng tuyển thành công." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
        }

        // FA-05: Duyệt đơn ứng tuyển
        [HttpPost("applications/{requestId}/review")]
        public async Task<IActionResult> ReviewApplication(int requestId, [FromBody] ReviewApplicationDto dto)
        {
            try
            {
                await _recruitmentService.ReviewApplicationAsync(GetCurrentAccountId(), requestId, dto);
                return Ok(new { Message = "Xử lý đơn ứng tuyển thành công." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
        }

        // FA-06: Mời trực tiếp Free Agent
        [HttpPost("invite")]
        public async Task<IActionResult> InvitePlayer([FromBody] InviteFreeAgentDto dto)
        {
            try
            {
                await _recruitmentService.InviteFreeAgentAsync(GetCurrentAccountId(), dto);
                return Ok(new { Message = "Đã gửi lời mời tới người chơi." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
        }
    }
}