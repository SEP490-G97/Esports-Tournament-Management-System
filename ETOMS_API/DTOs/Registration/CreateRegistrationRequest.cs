using System.ComponentModel.DataAnnotations;

namespace ETOMS_API.DTOs.Registration;

// STUB TẠM thay cho REG-01 (Nộp đơn đăng ký) của Cấn Việt Hùng — chỉ đủ để tạo
// một Registration cho đội, phục vụ các màn của Dũng (REG-03, REG-04, BRK-01..06)
// có dữ liệu để thao tác. KHÔNG phải bản đầy đủ của REG-01 (thiếu bước chọn lineup,
// upload minh chứng thẻ sinh viên...).
public class CreateRegistrationRequest
{
    [Required(ErrorMessage = "Vui lòng chọn giải đấu.")]
    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn giải đấu.")]
    public int TournamentId { get; set; }
}
