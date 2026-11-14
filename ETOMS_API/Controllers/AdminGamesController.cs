using ETOMS_API.DTOs.Admin;
using ETOMS_API.Services.Interface;
using Microsoft.AspNetCore.Mvc;

namespace ETOMS_API.Controllers
{
    [Route("api/admin/games")]
    public class AdminGamesController : AdminControllerBase
    {
        private const long MaxLogoBytes = 2 * 1024 * 1024;
        private const long MaxBannerBytes = 5 * 1024 * 1024;

        private readonly IAdminGameService _games;
        private readonly IImageStorage _images;

        public AdminGamesController(IAdminGameService games, IImageStorage images)
        {
            _games = games;
            _images = images;
        }

        [HttpGet]
        public async Task<IActionResult> List([FromQuery] GameListQuery query, CancellationToken ct)
        {
            var r = await _games.ListAsync(query, ct);
            return r.Error == GameError.None ? Ok(r.Data) : Map(r.Error);
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> Get(int id, CancellationToken ct)
        {
            var r = await _games.GetAsync(id, ct);
            return r.Error == GameError.None ? Ok(r.Data) : Map(r.Error);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] SaveGameRequest request, CancellationToken ct)
        {
            if (!TryGetActorId(out var actor)) return Unauthorized();
            var r = await _games.CreateAsync(actor, request, ct);
            return r.Error == GameError.None
                ? CreatedAtAction(nameof(Get), new { id = r.Data!.GameId }, r.Data)
                : Map(r.Error);
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] SaveGameRequest request, CancellationToken ct)
        {
            if (!TryGetActorId(out var actor)) return Unauthorized();
            var r = await _games.UpdateAsync(actor, id, request, ct);
            return r.Error == GameError.None ? Ok(r.Data) : Map(r.Error);
        }

        [HttpPost("{id:int}/deactivate")]
        public async Task<IActionResult> Deactivate(int id, CancellationToken ct)
        {
            if (!TryGetActorId(out var actor)) return Unauthorized();
            var r = await _games.SetActiveAsync(actor, id, false, ct);
            return r.Error == GameError.None ? Ok(r.Data) : Map(r.Error);
        }

        [HttpPost("{id:int}/activate")]
        public async Task<IActionResult> Activate(int id, CancellationToken ct)
        {
            if (!TryGetActorId(out var actor)) return Unauthorized();
            var r = await _games.SetActiveAsync(actor, id, true, ct);
            return r.Error == GameError.None ? Ok(r.Data) : Map(r.Error);
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id, CancellationToken ct)
        {
            if (!TryGetActorId(out var actor)) return Unauthorized();
            var r = await _games.DeleteAsync(actor, id, ct);
            if (r.Error != GameError.None) return Map(r.Error);
            _images.TryDelete(r.LogoUrl);
            _images.TryDelete(r.BannerUrl);
            return NoContent();
        }

        [HttpPost("{id:int}/logo")]
        [RequestSizeLimit(MaxLogoBytes + 512 * 1024)]
        public Task<IActionResult> UploadLogo(int id, IFormFile? file, CancellationToken ct) =>
            UploadAsync(id, GameImageKind.Logo, file, MaxLogoBytes, ct);

        [HttpPost("{id:int}/banner")]
        [RequestSizeLimit(MaxBannerBytes + 512 * 1024)]
        public Task<IActionResult> UploadBanner(int id, IFormFile? file, CancellationToken ct) =>
            UploadAsync(id, GameImageKind.Banner, file, MaxBannerBytes, ct);

        [HttpDelete("{id:int}/logo")]
        public Task<IActionResult> ClearLogo(int id, CancellationToken ct) => ClearAsync(id, GameImageKind.Logo, ct);

        [HttpDelete("{id:int}/banner")]
        public Task<IActionResult> ClearBanner(int id, CancellationToken ct) => ClearAsync(id, GameImageKind.Banner, ct);

        // ---------------------------------------------------------------- nội bộ

        private async Task<IActionResult> UploadAsync(int id, GameImageKind kind, IFormFile? file, long maxBytes, CancellationToken ct)
        {
            if (!TryGetActorId(out var actor)) return Unauthorized();

            var exists = await _games.GetAsync(id, ct);
            if (exists.Error != GameError.None) return Map(exists.Error);

            var saved = await _images.SaveAsync(file, $"games/{id}", maxBytes, ct);
            if (saved.Error != ImageError.None)
            {
                return saved.Error switch
                {
                    ImageError.Missing => Err(400, "FILE_REQUIRED", "Chưa chọn tệp ảnh."),
                    ImageError.TooLarge => Err(400, "FILE_TOO_LARGE",
                        $"Ảnh vượt quá {maxBytes / (1024 * 1024)} MB."),
                    _ => Err(400, "FILE_TYPE_INVALID", "Chỉ chấp nhận ảnh PNG, JPG hoặc WebP.")
                };
            }

            var r = await _games.SetImageAsync(actor, id, kind, saved.Url, ct);
            if (r.Error != GameError.None)
            {
                _images.TryDelete(saved.Url); // DB không cập nhật được thì bỏ file vừa lưu
                return Map(r.Error);
            }

            _images.TryDelete(r.OldUrl);
            return Ok(r.Data);
        }

        private async Task<IActionResult> ClearAsync(int id, GameImageKind kind, CancellationToken ct)
        {
            if (!TryGetActorId(out var actor)) return Unauthorized();
            var r = await _games.SetImageAsync(actor, id, kind, null, ct);
            if (r.Error != GameError.None) return Map(r.Error);
            _images.TryDelete(r.OldUrl);
            return Ok(r.Data);
        }

        private IActionResult Map(GameError e) => e switch
        {
            GameError.InvalidTitle => Err(400, "TITLE_INVALID", "Tên game phải từ 2 đến 150 ký tự."),
            GameError.InvalidShortCode => Err(400, "CODE_INVALID", "Mã game gồm 2–10 ký tự A–Z, 0–9 hoặc gạch dưới."),
            GameError.InvalidRoster => Err(400, "ROSTER_INVALID",
                "Số người chơi không hợp lệ: tối thiểu ≥ 1, tối đa ≥ tối thiểu (≤ 50), dự bị từ 0 đến 50."),
            GameError.InvalidFormat => Err(400, "FORMAT_INVALID", "Thể thức mặc định không hợp lệ."),
            GameError.InvalidEvidenceTypes => Err(400, "EVIDENCE_INVALID",
                "Chọn ít nhất một loại bằng chứng hợp lệ (IMAGE, VIDEO, REPLAY, DOCUMENT)."),
            GameError.InvalidFileSize => Err(400, "FILE_SIZE_INVALID", "Dung lượng bằng chứng tối đa phải từ 1 đến 100 MB."),
            GameError.InvalidFilter => Err(400, "INVALID_FILTER", "Bộ lọc trạng thái không hợp lệ."),
            GameError.NotFound => Err(404, "GAME_NOT_FOUND", "Không tìm thấy game."),
            GameError.TitleTaken => Err(409, "TITLE_TAKEN", "Tên game đã tồn tại."),
            GameError.CodeTaken => Err(409, "CODE_TAKEN", "Mã game đã tồn tại."),
            GameError.HasTournaments => Err(409, "GAME_HAS_TOURNAMENTS",
                "Game đã có giải đấu nên không thể xóa. Hãy vô hiệu hóa game thay vì xóa."),
            GameError.InUse => Err(409, "GAME_IN_USE",
                "Game đang được dùng bởi bộ luật, đội, bài tuyển quân hoặc hồ sơ người chơi nên không thể xóa. Hãy vô hiệu hóa game."),
            GameError.AlreadyInState => Err(409, "ALREADY_IN_STATE", "Game đã ở trạng thái này."),
            GameError.SaveFailed => Err(500, "SAVE_FAILED",
                "Không lưu được vào cơ sở dữ liệu. Chi tiết đã được ghi trong log máy chủ (thường do DB chưa chạy script cập nhật)."),
            _ => Err(500, "UNKNOWN", "Lỗi không xác định.")
        };
    }
}
