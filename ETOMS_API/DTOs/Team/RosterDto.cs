namespace ETOMS_API.DTOs.Team;

// TEAM-03: trang quản lý roster (v2).
public class RosterDto
{
    public int TeamId { get; set; }
    public int CaptainAccountId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Tag { get; set; } = string.Empty;
    public string GameTitle { get; set; } = string.Empty;

    public bool IsLocked { get; set; } // đội đang thi đấu giải ONGOING

    public int MaxStarters { get; set; }
    public int MaxSubstitutes { get; set; }

    public List<TeamMemberDto> Members { get; set; } = new();
    public List<TeamMemberDto> PendingMembers { get; set; } = new(); // đơn chờ duyệt

    public int StarterCount => Members.Count(m => m.Slot == "STARTER");
    public int SubCount => Members.Count(m => m.Slot == "SUBSTITUTE");
    public bool CanAddStarter => StarterCount < MaxStarters;
    public bool CanAddSub => SubCount < MaxSubstitutes;
}
