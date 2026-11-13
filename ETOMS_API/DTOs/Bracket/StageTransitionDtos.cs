namespace ETOMS_API.DTOs.Bracket;

// BRK-08: chuyển giai đoạn vòng bảng sang knockout của giải HYBRID.
public class StageTransitionBoardDto
{
    public int TournamentId { get; set; }
    public string TournamentName { get; set; } = string.Empty;
    public int? GroupCount { get; set; }
    public int? AdvancePerGroup { get; set; }
    // NOT_HYBRID, NOT_LOCKED, NO_GROUP_STAGE, IN_PROGRESS, NEEDS_DECISION, READY, DONE
    public string State { get; set; } = string.Empty;
    public string StateMessage { get; set; } = string.Empty;
    public int FinishedMatches { get; set; }
    public int TotalMatches { get; set; }
    public List<string> TieBreakerOrder { get; set; } = new();
    public List<GroupTableDto> Groups { get; set; } = new();
    // Các cặp vòng 1 dự kiến (READY) hoặc đã tạo (DONE).
    public List<KnockoutPairDto> Pairings { get; set; } = new();
    public bool CanAdvance { get; set; }
}

public class GroupTableDto
{
    public string GroupLabel { get; set; } = string.Empty;
    public List<GroupRowDto> Rows { get; set; } = new();
    public List<GroupTieDto> Ties { get; set; } = new();
}

public class GroupRowDto
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
    public bool Qualified { get; set; }
    public string? TieKey { get; set; }
}

// Nhóm đội hòa sau mọi tiêu chí. Blocking = ảnh hưởng tới suất đi tiếp hoặc hạt giống, cần Ban tổ chức xếp.
public class GroupTieDto
{
    public string TieKey { get; set; } = string.Empty;
    public string PositionsLabel { get; set; } = string.Empty;
    public bool Blocking { get; set; }
    public List<int> TeamIds { get; set; } = new();
    public List<string> TeamNames { get; set; } = new();
}

public class KnockoutPairDto
{
    public int PositionInRound { get; set; }
    public string TeamAName { get; set; } = string.Empty;
    public string TeamALabel { get; set; } = string.Empty;
    public string? TeamBName { get; set; }
    public string? TeamBLabel { get; set; }
    public bool IsBye { get; set; }
}

public class AdvanceStageRequest
{
    // Thứ tự Ban tổ chức chọn cho từng nhóm hòa: đội đứng trước xếp trên.
    public List<TieDecisionDto> Decisions { get; set; } = new();
}

public class TieDecisionDto
{
    public string GroupLabel { get; set; } = string.Empty;
    public List<int> TeamIds { get; set; } = new();
}
