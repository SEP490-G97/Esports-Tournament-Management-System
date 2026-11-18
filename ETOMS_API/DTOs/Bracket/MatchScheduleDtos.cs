using System.ComponentModel.DataAnnotations;

namespace ETOMS_API.DTOs.Bracket;

// BRK-12: xếp lịch thi đấu cho từng trận của nhánh đã khóa.
public class MatchScheduleBoardDto
{
    public int TournamentId { get; set; }
    public string TournamentName { get; set; } = string.Empty;
    public DateTime StartAt { get; set; }
    public DateTime? EndAt { get; set; }
    public bool CanEdit { get; set; }
    public string? BlockedReason { get; set; }
    public int MatchCount { get; set; }
    public int UnscheduledCount { get; set; }
    public List<ScheduleRoundDto> Rounds { get; set; } = new();
}

public class ScheduleRoundDto
{
    public int StageNo { get; set; }
    public int RoundNo { get; set; }
    public string Label { get; set; } = string.Empty;
    public List<ScheduleMatchDto> Matches { get; set; } = new();
}

public class ScheduleMatchDto
{
    public int MatchId { get; set; }
    public string Label { get; set; } = string.Empty;
    public string TeamAName { get; set; } = string.Empty;
    public string TeamBName { get; set; } = string.Empty;
    public DateTime? ScheduledAt { get; set; }
    public DateTime? CheckInOpenAt { get; set; }
    public string StatusLabel { get; set; } = string.Empty;
    public string StatusClass { get; set; } = string.Empty;
    public string? RefereeName { get; set; }
    public bool CanEdit { get; set; }
    public string? LockedReason { get; set; }
}

public class ScheduleMatchRequest
{
    // null = bỏ giờ đã xếp.
    public DateTime? ScheduledAt { get; set; }
}

public class ScheduleRoundRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "Thiếu giai đoạn.")]
    public int StageNo { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Thiếu vòng đấu.")]
    public int RoundNo { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn giờ trận đầu tiên.")]
    public DateTime? FirstAt { get; set; }

    [Range(0, 1440, ErrorMessage = "Khoảng cách giữa hai trận từ 0 đến 1440 phút.")]
    public int GapMinutes { get; set; }
}

public class ScheduleRoundResultDto
{
    public int ScheduledCount { get; set; }
    public List<string> Skipped { get; set; } = new();
    public MatchScheduleBoardDto Board { get; set; } = new();
}
