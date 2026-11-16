namespace ETOMS_API.DTOs.Team;

// TEAM-09 — Hồ sơ đội công khai: thông tin đội + roster + tỉ lệ thắng/thua + lịch sử giải
// đã tham gia + danh hiệu + điểm xếp hạng (vw_Leaderboard).
public class TeamDetailDto
{
    public int TeamId { get; set; }
    public int CaptainAccountId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Tag { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }
    public string GameTitle { get; set; } = string.Empty;
    public string CaptainName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public List<TeamMemberDto> Members { get; set; } = new();
    public int MemberCount => Members.Count;
    public int StarterCount => Members.Count(m => m.Slot == "STARTER");
    public int SubCount => Members.Count(m => m.Slot == "SUBSTITUTE");

    // TEAM-09: thành tích thi đấu.
    public int Wins { get; set; }
    public int Losses { get; set; }
    public double WinRate => (Wins + Losses) == 0 ? 0 : Math.Round((double)Wins / (Wins + Losses) * 100, 1);
    public int TitleCount { get; set; }
    public int RankingPoint { get; set; }
    public List<TeamTournamentHistoryDto> TournamentHistory { get; set; } = new();
}

public class TeamTournamentHistoryDto
{
    public int TournamentId { get; set; }
    public string TournamentName { get; set; } = string.Empty;
    public string TournamentStatus { get; set; } = string.Empty;
    public string FinalPlacement { get; set; } = string.Empty;
    public int? FinalRank { get; set; }
}
