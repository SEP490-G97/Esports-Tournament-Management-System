namespace ETOMS_API.DTOs.Bracket;

// BRK-13: bảng xếp hạng vòng bảng công khai.
public class GroupStandingBoardDto
{
    public int TournamentId { get; set; }
    public string TournamentName { get; set; } = string.Empty;
    public string FormatLabel { get; set; } = string.Empty;
    public int? AdvancePerGroup { get; set; }
    // NOT_GROUP_FORMAT, NO_GROUPS, IN_PROGRESS, FINISHED, ADVANCED
    public string State { get; set; } = string.Empty;
    public string StateMessage { get; set; } = string.Empty;
    public bool IsLocked { get; set; }
    public int FinishedMatches { get; set; }
    public int TotalMatches { get; set; }
    public List<string> TieBreakerOrder { get; set; } = new();
    public List<StandingGroupDto> Groups { get; set; } = new();
}

public class StandingGroupDto
{
    public string GroupLabel { get; set; } = string.Empty;
    public List<StandingRowDto> Rows { get; set; } = new();
    public List<StandingMatchDto> Matches { get; set; } = new();
}

public class StandingRowDto
{
    public int Position { get; set; }
    public int TeamId { get; set; }
    public string TeamName { get; set; } = string.Empty;
    public int Played { get; set; }
    public int Won { get; set; }
    public int Lost { get; set; }
    public int GamesWon { get; set; }
    public int GamesLost { get; set; }
    public int Diff { get; set; }
    public int Points { get; set; }
    // Tiêu chí đã phân định vị trí khi bằng điểm; null nếu tách được bằng điểm số.
    public string? DecidedBy { get; set; }
    // Còn bằng nhau sau mọi tiêu chí.
    public bool IsTied { get; set; }
    // QUALIFIED, PROVISIONAL, PENDING_DECISION, OUT hoặc null (giải không có suất đi tiếp).
    public string? Mark { get; set; }
}

public class StandingMatchDto
{
    public int MatchId { get; set; }
    public int RoundNo { get; set; }
    public string TeamAName { get; set; } = string.Empty;
    public string TeamBName { get; set; } = string.Empty;
    public int? ScoreA { get; set; }
    public int? ScoreB { get; set; }
    public bool IsDecided { get; set; }
    public string StatusLabel { get; set; } = string.Empty;
    public string StatusClass { get; set; } = string.Empty;
    public DateTime? ScheduledAt { get; set; }
}
