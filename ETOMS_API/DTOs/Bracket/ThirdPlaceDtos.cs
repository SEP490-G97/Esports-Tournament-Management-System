namespace ETOMS_API.DTOs.Bracket;

// BRK-09: trạng thái trận tranh hạng Ba của một giải.
public class ThirdPlaceStatusDto
{
    public int TournamentId { get; set; }
    public bool Enabled { get; set; }
    // DISABLED, NOT_APPLICABLE, NOT_LOCKED, NO_SEMIFINALS, WAITING, READY, PLAYED, CONFLICT
    public string State { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public int? MatchId { get; set; }
    public string? TeamAName { get; set; }
    public string? TeamBName { get; set; }
    public string? StatusLabel { get; set; }
    public string? WinnerName { get; set; }
}
