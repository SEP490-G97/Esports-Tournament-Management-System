using System.ComponentModel.DataAnnotations;

namespace ETOMS_API.DTOs.PickBan;

// GRL-07: kết quả cấm/chọn bản đồ của một trận. Ai cũng xem được; trọng tài của trận hoặc Ban tổ chức nhập.
public class MatchPickBanBoardDto
{
    public int MatchId { get; set; }
    public int TournamentId { get; set; }
    public string TournamentName { get; set; } = string.Empty;
    public string? RulesetName { get; set; }
    public string TeamAName { get; set; } = string.Empty;
    public string TeamBName { get; set; } = string.Empty;
    public int BestOf { get; set; }
    public string StatusLabel { get; set; } = string.Empty;
    public string StatusClass { get; set; } = string.Empty;
    public bool CanEdit { get; set; }
    // Lý do không nhập được, chỉ gửi cho người có quyền nhập.
    public string? BlockedReason { get; set; }
    public List<PickBanMapDto> Maps { get; set; } = new();
    public List<PickBanStepDto> Steps { get; set; } = new();
    public List<PickBanGameDto> Games { get; set; } = new();
    public string? RecordedByName { get; set; }
    public DateTime? RecordedAt { get; set; }
}

public class PickBanMapDto
{
    public int GameMapId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public bool IsActive { get; set; }
}

public class PickBanStepDto
{
    public int StepNo { get; set; }
    public string Action { get; set; } = string.Empty;
    public string ActionLabel { get; set; } = string.Empty;
    // "A", "B" hoặc null với bản đồ còn lại.
    public string? Side { get; set; }
    public string? TeamName { get; set; }
    public int GameMapId { get; set; }
    public string MapName { get; set; } = string.Empty;
}

// Bản đồ của từng ván, suy ra từ thứ tự các lượt chọn.
public class PickBanGameDto
{
    public int GameNo { get; set; }
    public string MapName { get; set; } = string.Empty;
    public string PickedBy { get; set; } = string.Empty;
}

public class SaveMatchPickBanRequest
{
    public List<PickBanStepInput> Steps { get; set; } = new();
}

public class PickBanStepInput
{
    [Required(ErrorMessage = "Thiếu loại lượt.")]
    public string Action { get; set; } = string.Empty;

    public string? Side { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn bản đồ.")]
    public int GameMapId { get; set; }
}
