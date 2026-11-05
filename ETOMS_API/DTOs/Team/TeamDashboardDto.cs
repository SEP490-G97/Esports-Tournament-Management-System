namespace ETOMS_API.DTOs.Team;

// TEAM-02: trang quản trị đội (chỉ đội trưởng). Sĩ số vs luật game, giải, thẻ phạt, mã mời.
public class TeamDashboardDto
{
    public int TeamId { get; set; }
    public int CaptainAccountId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Tag { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }
    public string GameTitle { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;

    public int ActiveMemberCount { get; set; }
    public int StarterCount { get; set; }
    public int SubCount { get; set; }

    // Luật game (lấy thẳng từ Game ở v2)
    public int MaxStarters { get; set; }
    public int MaxSubstitutes { get; set; }
    public int Capacity => MaxStarters + MaxSubstitutes;
    public bool IsUnderStrength => StarterCount < MaxStarters;

    public int TournamentCount { get; set; }
    public int PenaltyCount { get; set; }
    public int PendingCount { get; set; } // đơn ứng tuyển / lời mời đang chờ duyệt

    // Mã mời hiện tại của đội (v2: nằm trên Team)
    public string? InviteCode { get; set; }
    public DateTime? InviteExpiresAt { get; set; }

    public List<TeamMemberDto> Members { get; set; } = new();
}
