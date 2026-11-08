using System.ComponentModel.DataAnnotations;

namespace ETOMS_API.DTOs.GameRule;

// GRL-01: Luật thi đấu của một tựa game. Mỗi game một bộ luật, lưu ở chính bảng Game.
public class GameRuleDto
{
    public int GameId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string ShortCode { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public int MinStarters { get; set; }
    public int MaxStarters { get; set; }
    public int MaxSubstitutes { get; set; }
    public string DefaultFormat { get; set; } = string.Empty;
    public string AllowedEvidenceTypes { get; set; } = string.Empty;
    public int MaxEvidenceFileSizeMb { get; set; }

    // Số giải chưa kết thúc đang dùng game này. Các giải đó giữ số người của riêng giải (UC-43 E1).
    public int OpenTournamentCount { get; set; }
}

public class UpdateGameRuleRequest
{
    [Range(1, 100, ErrorMessage = "Số người chính thức tối thiểu phải từ 1 đến 100.")]
    public int MinStarters { get; set; }

    [Range(1, 100, ErrorMessage = "Số người chính thức tối đa phải từ 1 đến 100.")]
    public int MaxStarters { get; set; }

    [Range(0, 100, ErrorMessage = "Số dự bị phải từ 0 đến 100.")]
    public int MaxSubstitutes { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập đầy đủ thông tin bắt buộc.")]
    public string DefaultFormat { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập đầy đủ thông tin bắt buộc.")]
    [StringLength(200, ErrorMessage = "Danh sách định dạng tối đa 200 ký tự.")]
    public string AllowedEvidenceTypes { get; set; } = string.Empty;

    [Range(1, 100, ErrorMessage = "Dung lượng file tối đa phải từ 1 đến 100 MB.")]
    public int MaxEvidenceFileSizeMb { get; set; }
}
