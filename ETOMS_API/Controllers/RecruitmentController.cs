using ETOMS_API.DTOs.Recruitment;
using ETOMS_API.Services.Interface;
using ETOMS_API.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ETOMS_API.Controllers
{
    // STUB TẠM sửa lại kiểm quyền thay module FA (Nguyễn Quang Anh) — GetCurrentAccountId() cũ
    // đọc sai claim (luôn ra 0 cho mọi người, kể cả JWT thật), nên mọi hành động ở đây trước
    // giờ chạy như tài khoản 0 không tồn tại. Không phải bài làm cá nhân của Quang Anh.
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

        private bool TryGetAccountId(out int accountId)
        {
            accountId = _currentUser.UserId ?? 0;
            return _currentUser.IsAuthenticated;
        }

        [HttpPost("lfp")]
        public async Task<IActionResult> CreateLFP([FromBody] CreateLFPPostDto dto)
        {
            if (!TryGetAccountId(out var accountId)) return Unauthorized();
            try
            {
                int postId = await _recruitmentService.CreateLFPPostAsync(accountId, dto);
                return CreatedAtAction(nameof(CreateLFP), new { id = postId }, new { Message = "Đăng tin LFP thành công", PostId = postId });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Message = UserErrorMessage.From(ex) });
            }
        }

        [HttpPost("lft")]
        public async Task<IActionResult> CreateLFT([FromBody] CreateLFTPostDto dto)
        {
            if (!TryGetAccountId(out var accountId)) return Unauthorized();
            try
            {
                int postId = await _recruitmentService.CreateLFTPostAsync(accountId, dto);
                return CreatedAtAction(nameof(CreateLFT), new { id = postId }, new { Message = "Đăng tin LFT thành công", PostId = postId });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Message = UserErrorMessage.From(ex) });
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
            if (!TryGetAccountId(out var accountId)) return Unauthorized();
            try
            {
                await _recruitmentService.ApplyToLFPAsync(accountId, dto);
                return Ok(new { Message = "Gửi đơn ứng tuyển thành công." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Message = UserErrorMessage.From(ex) });
            }
        }

        [HttpPost("applications/{requestId}/review")]
        public async Task<IActionResult> ReviewApplication(int requestId, [FromBody] ReviewApplicationDto dto)
        {
            if (!TryGetAccountId(out var accountId)) return Unauthorized();
            try
            {
                await _recruitmentService.ReviewApplicationAsync(accountId, requestId, dto);
                return Ok(new { Message = "Xử lý đơn ứng tuyển thành công." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Message = UserErrorMessage.From(ex) });
            }
        }

        [HttpPost("invite")]
        public async Task<IActionResult> InvitePlayer([FromBody] InviteFreeAgentDto dto)
        {
            if (!TryGetAccountId(out var accountId)) return Unauthorized();
            try
            {
                await _recruitmentService.InviteFreeAgentAsync(accountId, dto);
                return Ok(new { Message = "Đã gửi lời mời tới người chơi." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Message = UserErrorMessage.From(ex) });
            }
        }
    }
}