using ETOMS.Models;
using Microsoft.AspNetCore.Mvc;

namespace ETOMS.Controllers;

// Trang lỗi tùy biến. Nhận mã lỗi từ pipeline (/Loi/404, /Loi/403, /Loi/500...)
// và hiển thị thông báo tiếng Việt thân thiện thay cho trang mặc định của trình duyệt.
[Route("Loi")]
public class LoiController : Controller
{
    [Route("{code:int}")]
    public IActionResult Index(int code)
    {
        Response.StatusCode = code; // giữ đúng mã lỗi HTTP

        var model = new ErrorPageViewModel
        {
            Code = code,
            Title = TitleFor(code),
            Message = MessageFor(code)
        };
        return View("Loi", model);
    }

    private static string TitleFor(int code) => code switch
    {
        400 => "Yêu cầu không hợp lệ",
        403 => "Không có quyền truy cập",
        404 => "Không tìm thấy trang",
        500 => "Lỗi hệ thống",
        _ => "Đã xảy ra lỗi"
    };

    private static string MessageFor(int code) => code switch
    {
        400 => "Dữ liệu gửi lên không hợp lệ. Vui lòng kiểm tra lại.",
        403 => "Bạn không có quyền xem nội dung này.",
        404 => "Đường dẫn bạn truy cập không tồn tại hoặc đã bị di chuyển.",
        500 => "Hệ thống gặp sự cố khi xử lý yêu cầu. Vui lòng thử lại sau.",
        _ => "Có lỗi xảy ra trong quá trình xử lý. Vui lòng thử lại."
    };
}
