using System.ComponentModel.DataAnnotations;

namespace ETOMS_API.DTOs.Tournament;

// GRL-06: Bộ luật của giải và các bộ luật còn dùng của tựa game để chọn.
public class TournamentRulesetBoardDto
{
    public int TournamentId { get; set; }
    public string TournamentName { get; set; } = string.Empty;
    public string GameTitle { get; set; } = string.Empty;
    public int StarterCount { get; set; }
    public int MaxSubstitutes { get; set; }
    public int? SelectedRulesetId { get; set; }
    public bool CanEdit { get; set; }
    public string? BlockedReason { get; set; }
    public List<RulesetOptionDto> Options { get; set; } = new();
}

public class RulesetOptionDto
{
    public int GameRulesetId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int MinStarters { get; set; }
    public int MaxStarters { get; set; }
    public int MaxSubstitutes { get; set; }
    public string DefaultFormat { get; set; } = string.Empty;
    public bool IsActive { get; set; }

    // Số người của giải nằm trong giới hạn của bộ luật thì mới chọn được.
    public bool FitsTournament { get; set; }
    public string? MismatchReason { get; set; }
}

public class SelectTournamentRulesetRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn bộ luật.")]
    public int GameRulesetId { get; set; }
}
