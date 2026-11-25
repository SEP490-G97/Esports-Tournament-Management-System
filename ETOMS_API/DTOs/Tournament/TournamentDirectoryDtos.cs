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

// POR-03 / UC-36 bước 5: chi tiết giải công khai.
public sealed record TournamentOverviewDto(
    int TournamentId,
    string Name,
    string Game,
    string Status,
    string StatusLabel,
    string StatusClass,
    bool RegistrationOpen,
    bool IsOrganizerView,
    string? Description,
    string? OrganizingUnit,
    string? ContactInfo,
    bool IsOnline,
    string? Region,
    string? Venue,
    string FormatLabel,
    int? GroupCount,
    int? AdvancePerGroup,
    bool HasThirdPlaceMatch,
    int MaxTeams,
    int StarterCount,
    int MaxSubstitutes,
    DateTime RegOpenAt,
    DateTime RegCloseAt,
    DateTime StartAt,
    DateTime? EndAt,
    decimal EntryFee,
    string? Prize,
    bool RequireIdentityDocument,
    bool RequirePaymentProof,
    bool RequireInGameName,
    int RulebookVersion,
    bool HasRulebook,
    bool BracketLocked,
    string? CancelReason,
    int ApprovedTeams,
    int WaitlistedTeams,
    string ImageUrl,
    // UC-46: số livestream đang phát — trang tổng quan hiện dải "Đang phát trực tiếp".
    int LiveStreams,
    // Số đội tối thiểu để khai mạc (TournamentLifecycle.MinTeams: 2, Loại kép 3) — trang tổng quan ghi khi giải chưa đủ đội.
    int MinTeams);

// POR-04: đội chính thức (đã duyệt, kể cả đội bị loại giữa giải — vẫn là đội đã tham dự) và đội trong danh sách chờ
// (chỉ tên đội — không lộ dữ liệu cá nhân thành viên).
public sealed record ParticipantDto(int TeamId, string Name, string Tag, string? LogoUrl, int? WaitlistPosition, int? FinalRank, string? PlacementLabel, bool Disqualified = false);

public sealed record TournamentParticipantsDto(
    int TournamentId,
    string TournamentName,
    int MaxTeams,
    IReadOnlyList<ParticipantDto> Approved,
    IReadOnlyList<ParticipantDto> Waitlisted,
    string Status);

public sealed record DirectoryGameOption(int GameId, string Title);

public sealed record TournamentDirectoryFilters(
    IReadOnlyList<DirectoryGameOption> Games,
    IReadOnlyList<string> Regions,
    bool HasOnline);
