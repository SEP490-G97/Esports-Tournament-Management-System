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
                return Fail(ex);
            }
        }

        // Quay lại bước 1 của trình tạo giải: sửa bản nháp đã có (không tạo giải mới).
        [HttpPut("{id}/step1")]
        public async Task<IActionResult> UpdateStep1(int id, [FromBody] Step1GeneralInfoDto dto)
        {
            var denied = await CheckCanManageAsync(id);
            if (denied is not null) return denied;
            try
            {
                await _setupService.UpdateStep1GeneralInfoAsync(id, _currentUser.UserId!.Value, dto);
                return Ok(new { Message = "Đã cập nhật thông tin giải.", TournamentId = id });
            }
            catch (Exception ex)
            {
                return Fail(ex);
            }
        }

        // Đơn vị tổ chức hiển thị cố định trên bước 1: tên tài khoản + đơn vị tổ chức đã duyệt tài khoản phụ trách.
        [HttpGet("organizer-identity")]
        public async Task<IActionResult> GetOrganizerIdentity()
        {
            if (!_currentUser.IsAuthenticated) return Unauthorized();
            if (!await _setupService.IsOrganizerOrAdminAsync(_currentUser.UserId!.Value))
                return StatusCode(403, new { Message = "Chỉ Ban tổ chức/Admin được tạo giải đấu." });
            return Ok(await _setupService.GetOrganizerIdentityAsync(_currentUser.UserId!.Value));
        }

        // Danh sách ngân hàng cho ô chọn "Ngân hàng nhận phí" (bước 4 và trang Sửa thông tin) — server kiểm cùng danh sách này.
        [HttpGet("banks")]
        public IActionResult GetBanks()
        {
            if (!_currentUser.IsAuthenticated) return Unauthorized();
            return Ok(ETOMS_API.Utils.BankAccount.Banks.Select(b => new { b.Code, b.Name }));
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
            catch (Exception ex)
            {
                return Fail(ex);
            }
        }

        // TOUR-05: các vòng knockout của giải + BO hiện tại để bước 3 hiện ô chọn cho từng vòng.
        [HttpGet("{id}/step3-bo-config")]
        public async Task<IActionResult> GetStep3(int id)
        {
            var denied = await CheckCanManageAsync(id);
            if (denied is not null) return denied;
            try
            {
                return Ok(await _setupService.GetStep3BoConfigAsync(id));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { Message = ex.Message });
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
                return Fail(ex);
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
                return Fail(ex);
            }
        }

        // Bước 6: lưu yêu cầu hồ sơ, giải vẫn ở trạng thái Bản nháp — chỉ người đã tạo giải hoặc Admin.
        [HttpPut("{id}/step6-requirements")]
        public async Task<IActionResult> SaveStep6(int id, [FromBody] Step5RequirementDto dto)
        {
            var denied = await CheckCanManageAsync(id);
            if (denied is not null) return denied;
            try
            {
                await _setupService.SaveStep6RequirementsAsync(id, dto);
                return Ok(new { Message = "Đã lưu giải ở trạng thái Bản nháp. Kiểm tra lại thông tin rồi xuất bản khi sẵn sàng." });
            }
            catch (Exception ex)
            {
                return Fail(ex);
            }
        }

        // TOUR-07 (bản cũ, giao diện không còn gọi): lưu yêu cầu hồ sơ và công bố ngay — chỉ người đã tạo giải hoặc Admin.
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
                return Fail(ex);
            }
        }

        // Lỗi nghiệp vụ (ArgumentException...) đã có thông báo tiếng Việt; lỗi ràng buộc DB thì
        // thông báo của provider là tiếng Anh — không đưa thẳng ra giao diện.
        private IActionResult Fail(Exception ex) => BadRequest(new
        {
            Message = Utils.UserErrorMessage.From(ex,
                "Không lưu được giải đấu: dữ liệu chưa hợp lệ (kiểm tra lại số đội, mốc thời gian, cấu hình vòng bảng).")
        });

        // Ảnh bìa giải: tệp tải lên (file) hoặc liên kết (url, kể cả link trang Google Ảnh). Dùng ở bước 1 trình tạo
        // giải và trang Sửa thông tin. Giải đã kết thúc/hủy thì không đổi.
        [HttpPost("{id}/banner")]
        [RequestSizeLimit(6 * 1024 * 1024)]
        public async Task<IActionResult> UploadBanner(int id, [FromForm] IFormFile? file, [FromForm] string? url,
            [FromServices] IWebHostEnvironment env, CancellationToken ct)
        {
            var denied = await CheckCanManageAsync(id);
            if (denied is not null) return denied;
            var (saved, error) = await ETOMS_API.Utils.BannerImage.SaveAsync(file, url, env, ct);
            if (saved is null) return BadRequest(new { Message = error });
            var ok = await _setupService.SetBannerAsync(id, saved);
            if (!ok)
            {
                ETOMS_API.Utils.ImageUploader.Delete(saved, env);
                return BadRequest(new { Message = "Giải đã kết thúc hoặc đã hủy nên không đổi ảnh bìa." });
            }
            return Ok(new { Message = "Đã cập nhật ảnh bìa.", BannerUrl = saved });
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