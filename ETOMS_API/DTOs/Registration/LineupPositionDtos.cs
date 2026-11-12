namespace ETOMS_API.DTOs.Registration;

// GRL-04: Đội hình của một đơn đăng ký kèm vị trí đã chọn cho từng người.
public class LineupPositionBoardDto
{
    public int RegistrationId { get; set; }
    public string TournamentName { get; set; } = string.Empty;
    public string TeamName { get; set; } = string.Empty;
    public string GameTitle { get; set; } = string.Empty;
    public string? RulesetName { get; set; }
    public string RegistrationStatusLabel { get; set; } = string.Empty;
    public bool CanEdit { get; set; }
    public string? BlockedReason { get; set; }

    // Bắt buộc mọi người chính thức có vị trí khi game khai đủ vị trí cho họ.
    public bool StarterPositionRequired { get; set; }

    public List<LineupPositionOptionDto> Positions { get; set; } = new();
    public List<LineupMemberDto> Members { get; set; } = new();
}

public class LineupPositionOptionDto
{
    public int GamePositionId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

public class LineupMemberDto
{
    public int RegistrationLineupId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string Slot { get; set; } = string.Empty;
    public string SlotLabel { get; set; } = string.Empty;
    public int? GamePositionId { get; set; }
}

public class SaveLineupPositionsRequest
{
    public List<LineupPositionItem> Items { get; set; } = new();
}

public class LineupPositionItem
{
    public int RegistrationLineupId { get; set; }

    // null = để trống.
    public int? GamePositionId { get; set; }
}
