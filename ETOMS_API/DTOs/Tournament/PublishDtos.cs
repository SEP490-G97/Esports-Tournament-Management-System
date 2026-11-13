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
}

public class PublishCheckDto
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public bool IsOk { get; set; }
    public string? Message { get; set; }
}
