namespace ETOMS_API.DTOs.Tournament;

// POR-02 / UC-36: tham số lọc danh sách giải công khai (đọc từ query string).
public sealed class TournamentDirectoryQuery
{
    public string? Q { get; set; }          // tìm theo tên giải hoặc đơn vị tổ chức
    public int? GameId { get; set; }
    public string? State { get; set; }      // REGISTRATION | UPCOMING | ONGOING | COMPLETED | CANCELLED
    public string? From { get; set; }       // yyyy-MM-dd, theo ngày khai mạc
    public string? To { get; set; }
    public string? Region { get; set; }     // tên khu vực, hoặc "ONLINE" = thi đấu trực tuyến
    public string? Fee { get; set; }        // FREE | PAID
    public string? Sort { get; set; }       // RELEVANT (mặc định) | START | START_DESC | NEWEST | NAME
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 12;
}

public sealed record TournamentCardDto(
    int TournamentId,
    string Name,
    int GameId,
    string Game,
    string Status,
    string StatusLabel,
    string StatusClass,
    bool RegistrationOpen,
    DateTime StartAt,
    DateTime? EndAt,
    DateTime RegCloseAt,
    string? Region,
    bool IsOnline,
    decimal EntryFee,
    string? Prize,
    string? OrganizingUnit,
    string FormatLabel,
    int Teams,
    int MaxTeams,
    string ImageUrl);

public sealed record TournamentDirectoryPage(
    IReadOnlyList<TournamentCardDto> Items,
    int Total,
    int Page,
    int PageSize,
    int TotalPages);

public sealed record DirectoryGameOption(int GameId, string Title);

public sealed record TournamentDirectoryFilters(
    IReadOnlyList<DirectoryGameOption> Games,
    IReadOnlyList<string> Regions,
    bool HasOnline);
