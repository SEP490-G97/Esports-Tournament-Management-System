using ETOMS_API.DTOs.Admin;
using ETOMS_API.Services.Interface;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ETOMS_API.Controllers;

[Route("api/admin/banners")]
public class AdminBannersController : AdminControllerBase
{
    private const long MaxImageBytes = 5 * 1024 * 1024;

    private readonly IAdminBannerService _svc;
    private readonly IImageStorage _images;

    public AdminBannersController(IAdminBannerService svc, IImageStorage images)
    {
        _svc = svc;
        _images = images;
    }

    /// <summary>GET /api/admin/banners?state=CURRENT|EXPIRED|ALL|LIVE|SCHEDULED|INACTIVE</summary>
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? state, CancellationToken ct)
    {
        var r = await _svc.ListAsync(state, ct);
        Response.Headers.CacheControl = "no-store";
        return r.Error == BannerError.None ? Ok(r.Data) : Map(r.Error);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] BannerRequest request, CancellationToken ct)
    {
        if (!TryGetActorId(out var actor)) return Unauthorized();
        var r = await _svc.CreateAsync(actor, request, ct);
        return r.Error == BannerError.None ? StatusCode(201, r.Item) : Map(r.Error);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] BannerRequest request, CancellationToken ct)
    {
        if (!TryGetActorId(out var actor)) return Unauthorized();
        var r = await _svc.UpdateAsync(actor, id, request, ct);
        if (r.Error != BannerError.None) return Map(r.Error);
        _images.TryDelete(r.OldImageUrl);
        return Ok(r.Item);
    }

    [HttpPost("{id:int}/activate")]
    public Task<IActionResult> Activate(int id, CancellationToken ct) => SetActive(id, true, ct);

    [HttpPost("{id:int}/deactivate")]
    public Task<IActionResult> Deactivate(int id, CancellationToken ct) => SetActive(id, false, ct);

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        if (!TryGetActorId(out var actor)) return Unauthorized();
        var r = await _svc.DeleteAsync(actor, id, ct);
        if (r.Error != BannerError.None) return Map(r.Error);
        _images.TryDelete(r.OldImageUrl);
        return NoContent();
    }

    [HttpPut("order")]
    public async Task<IActionResult> Reorder([FromBody] ReorderBannersRequest request, CancellationToken ct)
    {
        if (!TryGetActorId(out var actor)) return Unauthorized();
        var r = await _svc.ReorderAsync(actor, request?.Ids, ct);
        return r.Error == BannerError.None ? Ok(r.Items) : Map(r.Error);
    }

    /// <summary>Tải ảnh banner (PNG/JPEG/WebP ≤ 5MB), trả { url }. Chưa gắn vào banner nào cho tới khi lưu form.</summary>
    [HttpPost("image")]
    [RequestSizeLimit(MaxImageBytes + 512 * 1024)]
    public async Task<IActionResult> UploadImage(IFormFile? file, CancellationToken ct)
    {
        var saved = await _images.SaveAsync(file, "banners", MaxImageBytes, ct);
        return saved.Error switch
        {
            ImageError.None => Ok(new { url = saved.Url }),
            ImageError.Missing => Err(400, "FILE_MISSING", "Chưa chọn file ảnh."),
            ImageError.TooLarge => Err(413, "FILE_TOO_LARGE", "Ảnh banner tối đa 5MB."),
            _ => Err(400, "FILE_TYPE_INVALID", "Chỉ chấp nhận ảnh PNG, JPEG hoặc WebP.")
        };
    }

    private async Task<IActionResult> SetActive(int id, bool active, CancellationToken ct)
    {
        if (!TryGetActorId(out var actor)) return Unauthorized();
        var r = await _svc.SetActiveAsync(actor, id, active, ct);
        return r.Error == BannerError.None ? Ok(r.Item) : Map(r.Error);
    }

    private IActionResult Map(BannerError e) => e switch
    {
        BannerError.NotFound => Err(404, "BANNER_NOT_FOUND", "Không tìm thấy banner."),
        BannerError.InvalidTitle => Err(400, "TITLE_INVALID", "Tiêu đề phải từ 3 đến 200 ký tự."),
        BannerError.InvalidDescription => Err(400, "DESCRIPTION_INVALID", "Mô tả tối đa 500 ký tự."),
        BannerError.InvalidImage => Err(400, "IMAGE_INVALID", "Cần ảnh banner hợp lệ (đường dẫn nội bộ \"/...\" hoặc URL https)."),
        BannerError.InvalidLink => Err(400, "LINK_INVALID", "Liên kết phải là đường dẫn nội bộ bắt đầu bằng \"/\" hoặc URL https."),
        BannerError.InvalidType => Err(400, "TYPE_INVALID", "Loại banner phải là EVENT hoặc GIVEAWAY."),
        BannerError.InvalidPeriod => Err(400, "PERIOD_INVALID", "Thời gian kết thúc phải sau thời gian bắt đầu và (khi tạo mới) nằm trong tương lai."),
        BannerError.InvalidPosition => Err(400, "POSITION_INVALID", "Vị trí phải từ 1 trở lên."),
        BannerError.InvalidFilter => Err(400, "FILTER_INVALID", "Bộ lọc trạng thái không hợp lệ."),
        BannerError.TooManyBanners => Err(409, "TOO_MANY_BANNERS", "Đã đạt số banner đang áp dụng tối đa (50). Hãy xóa hoặc cho hết hạn bớt banner."),
        BannerError.AlreadyInState => Err(409, "ALREADY_IN_STATE", "Banner đã ở trạng thái này."),
        BannerError.ReorderMismatch => Err(409, "REORDER_MISMATCH", "Danh sách banner đã thay đổi. Hãy tải lại rồi sắp xếp lại."),
        _ => Err(500, "UNKNOWN", "Lỗi không xác định.")
    };
}