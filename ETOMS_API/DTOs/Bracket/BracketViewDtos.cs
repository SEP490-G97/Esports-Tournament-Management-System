namespace ETOMS_API.DTOs.Bracket;

// BRK-10, BRK-11: nhánh đấu công khai (sơ đồ cây và dạng danh sách dùng chung dữ liệu này).
public class BracketViewDto
{
    public int TournamentId { get; set; }
    public string TournamentName { get; set; } = string.Empty;
    public string FormatLabel { get; set; } = string.Empty;
    public string TournamentStatus { get; set; } = string.Empty;
    public bool IsLocked { get; set; }
    // Người xem là Ban tổ chức của giải: hiện nút khóa/mở khóa và chế độ đổi chỗ đội.
    public bool IsOrganizer { get; set; }
    // false: nhánh chưa khóa và người xem không phải Ban tổ chức (UC-20 E1), chỉ xem danh sách đội.
    public bool CanViewBracket { get; set; }
    public string? Notice { get; set; }
    public List<string> Participants { get; set; } = new();
    public bool HasGroupStage { get; set; }
    // Giải đang diễn ra thì trang tự làm mới.
    public bool IsLive { get; set; }
    // Đổi khi có trận đổi đội, giờ, tỷ số hoặc trạng thái; trang so để biết có cần vẽ lại.
    public string Version { get; set; } = string.Empty;
    public List<BracketStageViewDto> Stages { get; set; } = new();
}

public class BracketStageViewDto
{
    public int StageNo { get; set; }
    public string Label { get; set; } = string.Empty;
    public List<BracketRoundViewDto> Rounds { get; set; } = new();
    public List<BracketNodeViewDto> Nodes { get; set; } = new();
}

public class BracketRoundViewDto
{
    public int RoundNo { get; set; }
    public string Label { get; set; } = string.Empty;
}

public class BracketNodeViewDto
{
    public int NodeId { get; set; }
    public int? ParentNodeId { get; set; }
    public int RoundNo { get; set; }
    public int Position { get; set; }
    public string Label { get; set; } = string.Empty;
    public bool IsBye { get; set; }
    public bool IsThirdPlace { get; set; }
    public int? MatchId { get; set; }
    public BracketSlotDto TeamA { get; set; } = new();
    public BracketSlotDto TeamB { get; set; } = new();
    public int? ScoreA { get; set; }
    public int? ScoreB { get; set; }
    public int? WinnerTeamId { get; set; }
    public string? Status { get; set; }
    public string? StatusLabel { get; set; }
    public string? StatusClass { get; set; }
    public DateTime? ScheduledAt { get; set; }
    public int BestOf { get; set; }
    public string? RefereeName { get; set; }
    public List<BracketGameDto> Games { get; set; } = new();
}

public class BracketSlotDto
{
    public int? TeamId { get; set; }
    // Tên đội, hoặc mô tả chỗ trống: "Thắng Bán kết · Trận 1", "Thua …", "Chưa xác định".
    public string Name { get; set; } = string.Empty;
}

public class BracketGameDto
{
    public int GameNo { get; set; }
    public int? ScoreA { get; set; }
    public int? ScoreB { get; set; }
}
