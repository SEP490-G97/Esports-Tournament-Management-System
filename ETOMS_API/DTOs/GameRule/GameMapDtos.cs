using System.ComponentModel.DataAnnotations;

namespace ETOMS_API.DTOs.GameRule;

// GRL-02: Một bản đồ trong kho bản đồ của một bộ luật.
public class GameMapDto
{
    public int GameMapId { get; set; }
    public int GameRulesetId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

// Ảnh gửi kèm dạng multipart (Image). Khi sửa, không gửi ảnh thì giữ ảnh cũ.
public class SaveGameMapRequest
{
    [Required(ErrorMessage = "Vui lòng nhập đầy đủ thông tin bắt buộc.")]
    [StringLength(100, ErrorMessage = "Tên bản đồ tối đa 100 ký tự.")]
    public string Name { get; set; } = string.Empty;

    // ACTIVE = còn dùng, RETIRED = đã loại. Bỏ trống khi thêm mới.
    public string? Status { get; set; }
}
