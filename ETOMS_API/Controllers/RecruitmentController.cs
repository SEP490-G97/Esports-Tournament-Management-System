using System.Security.Claims;
using ETOMS_API.DTOs.Recruitment;
using ETOMS_API.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ETOMS_API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class RecruitmentController : ControllerBase
    {
        private readonly IRecruitmentService _recruitmentService;
        private readonly ICurrentUser _currentUser;

        public RecruitmentController(IRecruitmentService recruitmentService, ICurrentUser currentUser)
        {
            _recruitmentService = recruitmentService;
            _currentUser = currentUser;
        }

        private int GetCurrentAccountId()
        {
            if (_currentUser.IsAuthenticated && _currentUser.UserId.HasValue)
            {
                return _currentUser.UserId.Value;
            }

            var subClaim = User.FindFirst("sub")?.Value 
                        ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(subClaim, out var id) ? id : 0;
        }

        [HttpPost("lfp")]
        public async Task<IActionResult> CreateLFP([FromBody] CreateLFPPostDto dto)
        {
            int accountId = GetCurrentAccountId();
            if (accountId <= 0) return Unauthorized(new { Message = "Bạn cần đăng nhập để thực hiện thao tác này." });

            try
            {
                int postId = await _recruitmentService.CreateLFPPostAsync(accountId, dto);
                return CreatedAtAction(nameof(CreateLFP), new { id = postId }, new { Message = "Đăng tin LFP thành công", PostId = postId });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
        }

        [HttpPost("lft")]
        public async Task<IActionResult> CreateLFT([FromBody] CreateLFTPostDto dto)
        {
            int accountId = GetCurrentAccountId();
            if (accountId <= 0) return Unauthorized(new { Message = "Bạn cần đăng nhập để thực hiện thao tác này." });

            try
            {
                int postId = await _recruitmentService.CreateLFTPostAsync(accountId, dto);
                return CreatedAtAction(nameof(CreateLFT), new { id = postId }, new { Message = "Đăng tin LFT thành công", PostId = postId });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
        }

        [HttpGet("board")]
        [AllowAnonymous]
        public async Task<IActionResult> GetBoard([FromQuery] RecruitmentBoardQueryDto query)
        {
            var result = await _recruitmentService.GetRecruitmentBoardAsync(query);
            return Ok(result);
        }

        [HttpPost("apply")]
        public async Task<IActionResult> Apply([FromBody] ApplyLFPPostDto dto)
        {
            int accountId = GetCurrentAccountId();
            if (accountId <= 0) return Unauthorized(new { Message = "Bạn cần đăng nhập để gửi đơn ứng tuyển." });

            try
            {
                await _recruitmentService.ApplyToLFPAsync(accountId, dto);
                return Ok(new { Message = "Gửi đơn ứng tuyển thành công." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
        }

        [HttpPost("applications/{requestId}/review")]
        public async Task<IActionResult> ReviewApplication(int requestId, [FromBody] ReviewApplicationDto dto)
        {
            int accountId = GetCurrentAccountId();
            if (accountId <= 0) return Unauthorized(new { Message = "Bạn cần đăng nhập để xử lý đơn ứng tuyển." });

            try
            {
                await _recruitmentService.ReviewApplicationAsync(accountId, requestId, dto);
                return Ok(new { Message = "Xử lý đơn ứng tuyển thành công." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
        }

        [HttpPost("invite")]
        public async Task<IActionResult> InvitePlayer([FromBody] InviteFreeAgentDto dto)
        {
            int accountId = GetCurrentAccountId();
            if (accountId <= 0) return Unauthorized(new { Message = "Bạn cần đăng nhập để mời tuyển thủ." });

            try
            {
                await _recruitmentService.InviteFreeAgentAsync(accountId, dto);
                return Ok(new { Message = "Đã gửi lời mời tới người chơi." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
        }
    }
}