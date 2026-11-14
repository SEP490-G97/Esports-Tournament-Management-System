using Microsoft.EntityFrameworkCore;

namespace ETOMS_API.Utils;

// Thông báo lỗi đưa ra giao diện: lỗi nghiệp vụ (ArgumentException, InvalidOperationException...) đã viết
// tiếng Việt thì giữ nguyên; lỗi ràng buộc database thì thông báo của provider là tiếng Anh, không lộ ra.
public static class UserErrorMessage
{
    public static string From(Exception ex, string databaseFallback = "Không lưu được dữ liệu: dữ liệu chưa hợp lệ hoặc trùng với dữ liệu đã có.")
        => ex is DbUpdateException ? databaseFallback : ex.Message;
}
