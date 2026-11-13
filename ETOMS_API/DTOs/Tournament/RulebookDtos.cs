using System.ComponentModel.DataAnnotations;

namespace ETOMS_API.DTOs.Tournament;

// TOUR-08: Nội dung thể lệ (HTML đã lọc) của một phiên bản.
public class RulebookDto
{
    public int TournamentId { get; set; }
    public string TournamentName { get; set; } = string.Empty;
    public string TournamentStatus { get; set; } = string.Empty;
    public int Version { get; set; }
    public int CurrentVersion { get; set; }
    public string? Content { get; set; }
    public bool CanEdit { get; set; }
}

// Một dòng trong lịch sử phiên bản. SavedAt = null khi bản đó được lưu trước khi có nhật ký.
public class RulebookVersionDto
{
    public int Version { get; set; }
    public DateTime? SavedAt { get; set; }
    public string? SavedByName { get; set; }
    public bool IsCurrent { get; set; }
}

public class SaveRulebookRequest
{
    [Required(ErrorMessage = "Vui lòng nhập đầy đủ thông tin bắt buộc.")]
    [StringLength(100000, ErrorMessage = "Thể lệ quá dài, tối đa 100.000 ký tự.")]
    public string Content { get; set; } = string.Empty;

    // Phiên bản người dùng đang sửa, để không ghi đè bản mới hơn lưu từ tab khác.
    [Range(1, int.MaxValue, ErrorMessage = "Phiên bản thể lệ không hợp lệ.")]
    public int BaseVersion { get; set; }
}

public class RulebookSavedDto
{
    public int Version { get; set; }
    public bool Changed { get; set; }
    public int NotifiedCaptains { get; set; }
}
