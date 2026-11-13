namespace ETOMS_API.DTOs.Tournament;

// TOUR-12: Số liệu của một giải cho Ban tổ chức. Trang chỉ đọc, không thay đổi dữ liệu.
public class TournamentDashboardDto
{
    public int TournamentId { get; set; }
    public string TournamentName { get; set; } = string.Empty;
    public string StatusLabel { get; set; } = string.Empty;
    public string StatusClass { get; set; } = string.Empty;

    public int MaxTeams { get; set; }
    public int PendingCount { get; set; }
    public int ApprovedCount { get; set; }
    public int WaitlistedCount { get; set; }
    public int RemainingSlots { get; set; }
    public List<DashboardCountDto> RegistrationsByStatus { get; set; } = new();

    public int TotalMatches { get; set; }
    public List<DashboardCountDto> MatchesByStatus { get; set; } = new();

    // Tiến độ của các trận đang trong cửa sổ check-in.
    public int CheckInMatchCount { get; set; }
    public int CheckedInTeamCount { get; set; }
    public int ExpectedTeamCount { get; set; }

    public int OpenDisputeCount { get; set; }
    public int PendingAppealCount { get; set; }

    // Số tiền đội khai trên biên lai mà Ban tổ chức đã xác nhận, không phải giao dịch thật (BR-FIN-01).
    public bool HasEntryFee { get; set; }
    public decimal ConfirmedFeeTotal { get; set; }
    public int ConfirmedPaymentCount { get; set; }
    public int MissingAmountCount { get; set; }

    public List<DashboardRegistrationDto> PendingRegistrations { get; set; } = new();
    public List<DashboardMatchDto> TodayMatches { get; set; } = new();
    public List<DashboardDisputeDto> OpenDisputes { get; set; } = new();
}

public class DashboardCountDto
{
    public string Label { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class DashboardRegistrationDto
{
    public int RegistrationId { get; set; }
    public string TeamName { get; set; } = string.Empty;
    public DateTime? SubmittedAt { get; set; }
}

public class DashboardMatchDto
{
    public int MatchId { get; set; }
    public string TeamAName { get; set; } = string.Empty;
    public string TeamBName { get; set; } = string.Empty;
    public DateTime? ScheduledAt { get; set; }
    public string StatusLabel { get; set; } = string.Empty;
    public string StatusClass { get; set; } = string.Empty;
}

public class DashboardDisputeDto
{
    public int DisputeId { get; set; }
    public int MatchId { get; set; }
    public string TeamAName { get; set; } = string.Empty;
    public string TeamBName { get; set; } = string.Empty;
    public string ReasonLabel { get; set; } = string.Empty;
    public bool HasPendingAppeal { get; set; }
    public DateTime OpenedAt { get; set; }
}
