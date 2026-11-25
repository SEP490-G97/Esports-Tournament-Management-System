using ETOMS_API.Utils;
using Microsoft.AspNetCore.Mvc;

namespace ETOMS_API.Controllers;

[ApiController]
public abstract class ApiControllerBase : ControllerBase
{
    protected IActionResult FromResult(Result result)
        => result.IsSuccess
            ? Ok(new { message = result.Message })
            : StatusCode(result.StatusCode, new { message = result.Message, fieldErrors = result.FieldErrors });

    protected IActionResult FromResult<T>(Result<T> result)
        => result.IsSuccess
            ? Ok(new { message = result.Message, data = result.Data })
            : StatusCode(result.StatusCode, new { message = result.Message, fieldErrors = result.FieldErrors });

    // Trả tệp riêng tư (biên lai, giấy tờ) — mở thẳng trên trình duyệt nếu là ảnh/PDF; không có tệp
    // trên máy chủ này thì 404 kèm thông báo, không để lỗi 500.
    protected IActionResult PrivateFile(PrivateFileStorage storage, string relativePath, string fileName)
    {
        var stream = storage.TryOpenRead(relativePath);
        if (stream is null)
            return NotFound(new
            {
                message = "Không tìm thấy tệp trên máy chủ. Tệp có thể đã bị xóa hoặc chỉ được tải lên ở máy chủ khác.",
                fieldErrors = new Dictionary<string, string>()
            });
        var type = PrivateFileStorage.ContentTypeOf(fileName);
        return type == "application/octet-stream"
            ? File(stream, type, fileName)
            : File(stream, type, enableRangeProcessing: false);
    }
}
