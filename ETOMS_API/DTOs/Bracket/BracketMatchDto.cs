namespace ETOMS_API.DTOs.Bracket;

// 1 trận trong cây nhánh đấu — dùng để hiển thị danh sách theo vòng.
public class BracketMatchDto
{
    public int BracketNodeId { get; set; }
    public int? MatchId { get; set; }
    public string StageType { get; set; } = string.Empty; // GROUP | KNOCKOUT
    public string? GroupLabel { get; set; }
    public int RoundNo { get; set; }
    public int PositionInRound { get; set; }
    public bool IsBye { get; set; }

    public int? TeamAId { get; set; }
    public string? TeamAName { get; set; }
    public int? TeamBId { get; set; }
    public string? TeamBName { get; set; }
    public int? WinnerTeamId { get; set; }
    public string? WinnerTeamName { get; set; }

    public string RoundLabel => StageType == "GROUP"
        ? $"Bảng {GroupLabel} · Vòng {RoundNo}"
        : $"Vòng {RoundNo}";

    public bool IsReadyToPick => !IsBye && TeamAId.HasValue && TeamBId.HasValue && !WinnerTeamId.HasValue;
}

public class BracketTreeDto
{
    public int TournamentId { get; set; }
    public string TournamentName { get; set; } = string.Empty;
    public string Format { get; set; } = string.Empty;
    public string TournamentStatus { get; set; } = string.Empty;
    public bool IsLocked { get; set; }
    public DateTime? LockedAt { get; set; }
    public int ApprovedTeamCount { get; set; }
    public List<BracketMatchDto> Matches { get; set; } = new();

    public bool HasBracket => Matches.Count > 0;

    public IEnumerable<IGrouping<string, BracketMatchDto>> Grouped
        => Matches.OrderBy(m => m.StageType).ThenBy(m => m.GroupLabel).ThenBy(m => m.RoundNo).ThenBy(m => m.PositionInRound)
            .GroupBy(m => m.RoundLabel);

    // Trận loại trực tiếp — vẽ dạng cây có đường nối (xem _BracketTree.cshtml).
    // Trận vòng bảng (GROUP, round-robin) không có quan hệ cha-con nên vẫn hiển thị danh sách theo vòng/bảng ở Grouped.
    public List<BracketMatchDto> KnockoutMatches
        => Matches.Where(m => m.StageType == "KNOCKOUT").OrderBy(m => m.RoundNo).ThenBy(m => m.PositionInRound).ToList();
}

// Dữ liệu truyền vào partial _BracketTree — tách riêng vì Manage (có form ghi nhận thắng)
// và View (chỉ xem) dùng chung một cách vẽ cây.
public class KnockoutTreeViewModel
{
    public int TournamentId { get; set; }
    public List<BracketMatchDto> Matches { get; set; } = new();
    public bool AllowActions { get; set; }
}
