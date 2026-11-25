namespace ETOMS_API.DTOs.Tournament;

// TOUR-09: Tình trạng xuất bản của một giải và các điều kiện còn thiếu.
public class PublishStatusDto
{
    public int TournamentId { get; set; }
    public string TournamentName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string StatusLabel { get; set; } = string.Empty;
    public string StatusClass { get; set; } = string.Empty;
    public bool CanPublish { get; set; }
    public bool CanUnpublish { get; set; }
    public string? UnpublishBlockedReason { get; set; }
    public List<PublishCheckDto> Checks { get; set; } = new();

    // Thanh giai đoạn trên trang Trạng thái & xuất bản.
    public string Format { get; set; } = string.Empty;
    public DateTime RegOpenAt { get; set; }
    public DateTime RegCloseAt { get; set; }
    public DateTime StartAt { get; set; }
    public DateTime? EndAt { get; set; }
    public int MaxTeams { get; set; }
    public int ApprovedCount { get; set; }
    public int PendingCount { get; set; }
    public bool BracketGenerated { get; set; }
    public bool BracketLocked { get; set; }
    public int MatchTotal { get; set; }
    public int MatchDone { get; set; }
    public int OpenDisputes { get; set; }
}

public class PublishCheckDto
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public bool IsOk { get; set; }
    public string? Message { get; set; }
}
