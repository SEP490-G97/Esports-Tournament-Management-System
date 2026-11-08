using System.ComponentModel.DataAnnotations;

namespace ETOMS_API.DTOs.GameRule;

// GRL-03: Một vị trí trong đội của tựa game, ví dụ Đi rừng.
public class GamePositionDto
{
    public int GamePositionId { get; set; }
    public int GameId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

public class SaveGamePositionRequest
{
    [Required(ErrorMessage = "Vui lòng nhập đầy đủ thông tin bắt buộc.")]
    [StringLength(50, ErrorMessage = "Tên vị trí tối đa 50 ký tự.")]
    public string Name { get; set; } = string.Empty;

    // ACTIVE = còn dùng, RETIRED = đã loại. Bỏ trống khi thêm mới hoặc để giữ nguyên.
    public string? Status { get; set; }
}

public class MoveGamePositionRequest
{
    // "up" hoặc "down".
    [Required(ErrorMessage = "Hướng di chuyển không hợp lệ.")]
    public string Direction { get; set; } = string.Empty;
}
