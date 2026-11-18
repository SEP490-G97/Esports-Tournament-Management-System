using System.ComponentModel.DataAnnotations;

namespace ETOMS_API.DTOs.Bracket;

// BRK-07: BTC cấu hình thứ tự ưu tiên tiêu chí phân định khi các đội bằng điểm trong vòng
// bảng. 4 tiêu chí cố định theo catalogue — chỉ đổi THỨ TỰ ưu tiên, không thêm/bớt tiêu chí.
// Lưu thẳng chuỗi tiếng Việt vào Tournament.TieBreakerOrder (NVARCHAR, không ràng buộc CHECK),
// không mã hoá qua key tiếng Anh — khớp đúng dữ liệu demo sẵn có trong DB.
public static class TieBreakerCriteria
{
    // Thứ tự mặc định khi giải chưa cấu hình (đúng theo catalogue BRK-07).
    public static readonly IReadOnlyList<string> DefaultOrder = new[]
    {
        "Đối đầu trực tiếp",
        "Hiệu số ván",
        "Tổng ván thắng",
        "Bốc thăm"
    };
}

public class TieBreakerConfigDto
{
    public int TournamentId { get; set; }
    public List<string> Order { get; set; } = new();
    public bool IsDefault { get; set; } // true nếu giải chưa cấu hình, đang hiện thứ tự mặc định
}

public class SetTieBreakerRequest
{
    [Required(ErrorMessage = "Vui lòng chọn đủ 4 tiêu chí theo thứ tự ưu tiên.")]
    [MinLength(4, ErrorMessage = "Phải có đúng 4 tiêu chí.")]
    [MaxLength(4, ErrorMessage = "Phải có đúng 4 tiêu chí.")]
    public List<string> Order { get; set; } = new();
}
