using System.ComponentModel.DataAnnotations;

namespace ETOMS_API.DTOs.GameRule;

// GRL-01: Tựa game kèm các bộ luật thi đấu của nó (một game có nhiều bộ luật, ví dụ Draft Pick, Blind Pick).
public class GameWithRulesetsDto
{
    public int GameId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string ShortCode { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public List<GameRulesetDto> Rulesets { get; set; } = new();
}

// Danh sách game luôn đọc được; bộ luật đọc lỗi (bảng chưa có) thì RulesetError có nội dung.
public class GameRuleBoardDto
{
    public List<GameWithRulesetsDto> Games { get; set; } = new();
    public string? RulesetError { get; set; }
}

public class GameRulesetDto
{
    public int GameRulesetId { get; set; }
    public int GameId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int MinStarters { get; set; }
    public int MaxStarters { get; set; }
    public int MaxSubstitutes { get; set; }
    public string DefaultFormat { get; set; } = string.Empty;
    public string AllowedEvidenceTypes { get; set; } = string.Empty;
    public int MaxEvidenceFileSizeMb { get; set; }
    public bool IsActive { get; set; }

    // Số giải chưa kết thúc đang dùng bộ luật này. Các giải đó giữ số người của riêng giải (UC-43 E1).
    public int OpenTournamentCount { get; set; }
}

public class SaveGameRulesetRequest
{
    [Required(ErrorMessage = "Vui lòng nhập đầy đủ thông tin bắt buộc.")]
    [StringLength(100, ErrorMessage = "Tên bộ luật tối đa 100 ký tự.")]
    public string Name { get; set; } = string.Empty;

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

    // ACTIVE = còn dùng, RETIRED = đã loại. Bỏ trống khi tạo mới hoặc để giữ nguyên.
    public string? Status { get; set; }
}
