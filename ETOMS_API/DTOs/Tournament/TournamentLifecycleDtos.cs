namespace ETOMS_API.DTOs.Tournament;

// Thẻ "Tiến trình giải" ở bảng điều hành của Ban tổ chức (mục 28).
public class TournamentLifecycleDto
{
    public int TournamentId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string LabelClass { get; set; } = string.Empty;
    // Câu giải thích bước hiện tại và việc BTC nên làm tiếp.
    public string Hint { get; set; } = string.Empty;

    public int ApprovedTeams { get; set; }
    public int MinTeams { get; set; }
    public int MaxTeams { get; set; }
    public bool RegistrationOpen { get; set; }
    public bool ShortOfTeams { get; set; }
    public bool HasBracket { get; set; }
    public bool BracketLocked { get; set; }
    public int MatchesTotal { get; set; }
    public int MatchesFinished { get; set; }

    public DateTime RegOpenAt { get; set; }
    public DateTime RegCloseAt { get; set; }
    public DateTime StartAt { get; set; }
    public DateTime? EndAt { get; set; }

    public bool CanStart { get; set; }
    public string? StartBlocked { get; set; }
    public bool CanFinish { get; set; }
    public string? FinishBlocked { get; set; }
    public bool CanExtend { get; set; }
    public string? ExtendBlocked { get; set; }

    // Vòng tròn: các đội đứng đầu đang bằng nhau mọi chỉ số — BTC chọn thứ tự khi kết thúc giải.
    public List<TieTeamDto> TiedTop { get; set; } = new();
    // Sau khi kết thúc: 3 hạng đầu.
    public List<FinalRankDto> Podium { get; set; } = new();
}

public class TieTeamDto
{
    public int TeamId { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class FinalRankDto
{
    public int Rank { get; set; }
    public int TeamId { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class ExtendRegistrationRequest
{
    public DateTime? RegCloseAt { get; set; }
    public DateTime? StartAt { get; set; }
    public DateTime? EndAt { get; set; }
}

public class FinishTournamentRequest
{
    // Thứ tự các đội đang bằng nhau ở hạng 1–2 (chỉ cần khi TiedTop có đội).
    public List<int>? TieOrder { get; set; }
}
