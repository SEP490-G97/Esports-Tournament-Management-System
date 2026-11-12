using System.ComponentModel.DataAnnotations;

namespace ETOMS_API.DTOs.Tournament;

// TOUR-13: Bảng phân công trọng tài của một giải.
public class RefereeBoardDto
{
    public int TournamentId { get; set; }
    public string TournamentName { get; set; } = string.Empty;
    public bool IsBracketLocked { get; set; }
    public bool CanAssign { get; set; }
    public string? BlockedReason { get; set; }
    public List<RefereeRoundDto> Rounds { get; set; } = new();
    public List<RefereeMatchDto> Matches { get; set; } = new();
}

public class RefereeRoundDto
{
    public int StageNo { get; set; }
    public int RoundNo { get; set; }
    public string Label { get; set; } = string.Empty;
}

public class RefereeMatchDto
{
    public int MatchId { get; set; }
    public string RoundLabel { get; set; } = string.Empty;
    public string TeamAName { get; set; } = string.Empty;
    public string TeamBName { get; set; } = string.Empty;
    public DateTime? ScheduledAt { get; set; }
    public string StatusLabel { get; set; } = string.Empty;
    public string StatusClass { get; set; } = string.Empty;
    public int? RefereeAccountId { get; set; }
    public string? RefereeName { get; set; }
    public bool CanAssign { get; set; }
}

public class RefereeCandidateDto
{
    public int AccountId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

// RefereeAccountId = null để bỏ trọng tài, quyền phân xử khi đó thuộc Ban tổ chức (BR-MATCH-03).
public class AssignRefereeRequest
{
    public int? RefereeAccountId { get; set; }
}

public class AssignRoundRefereeRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "Vòng đấu không hợp lệ.")]
    public int StageNo { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Vòng đấu không hợp lệ.")]
    public int RoundNo { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn trọng tài.")]
    public int RefereeAccountId { get; set; }
}

public class AssignRoundResultDto
{
    public int AssignedCount { get; set; }
    public List<string> SkippedMatches { get; set; } = new();
}
