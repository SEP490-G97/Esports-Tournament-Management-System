namespace ETOMS_API.DTOs.Discipline;

// UC-34 / UC-35 (module PEN): thẻ phạt, hủy thẻ phạt bằng dòng liên kết, ngưỡng loại tự động.

public sealed class IssuePenaltyRequest
{
    public int TeamId { get; set; }
    public int? MatchId { get; set; }
    public string Severity { get; set; } = string.Empty;   // WARNING | MINOR | MAJOR
    public int Points { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public sealed class CancelPenaltyRequest
{
    public string Reason { get; set; } = string.Empty;
}

public sealed class SetPenaltyThresholdRequest
{
    // null = không áp ngưỡng loại tự động.
    public int? Threshold { get; set; }
}

public sealed class PenaltyDto
{
    public int PenaltyId { get; set; }
    public int TournamentId { get; set; }
    public string TournamentName { get; set; } = string.Empty;
    public int TeamId { get; set; }
    public string TeamName { get; set; } = string.Empty;
    public int? MatchId { get; set; }
    public string Severity { get; set; } = string.Empty;
    public string SeverityLabel { get; set; } = string.Empty;
    public int Points { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string StatusLabel { get; set; } = string.Empty;
    public string IssuedByName { get; set; } = string.Empty;
    public DateTime IssuedAt { get; set; }
    // Dòng hủy: trỏ tới thẻ bị hủy và lý do hủy (BR-PEN-03).
    public int? CancelsPenaltyId { get; set; }
    public string? CancelReason { get; set; }
    // Thẻ gốc đã bị hủy bởi dòng nào.
    public int? CancelledByPenaltyId { get; set; }
    public bool CanCancel { get; set; }
}

public sealed class TeamPenaltyTotalDto
{
    public int TeamId { get; set; }
    public string TeamName { get; set; } = string.Empty;
    public string RegistrationStatus { get; set; } = string.Empty;
    public string RegistrationStatusLabel { get; set; } = string.Empty;
    public int ActivePoints { get; set; }
    public bool CanIssue { get; set; }
}

public sealed class DisciplineBoardDto
{
    public int TournamentId { get; set; }
    public string TournamentName { get; set; } = string.Empty;
    public string TournamentStatus { get; set; } = string.Empty;
    public int? PenaltyThreshold { get; set; }
    public bool IsOrganizer { get; set; }
    public bool CanIssue { get; set; }
    public bool CanSetThreshold { get; set; }
    public List<TeamPenaltyTotalDto> Teams { get; set; } = new();
    public List<PenaltyDto> Penalties { get; set; } = new();
}

public sealed class TeamDisciplineRecordDto
{
    public int TeamId { get; set; }
    public List<PenaltyDto> Penalties { get; set; } = new();
}
