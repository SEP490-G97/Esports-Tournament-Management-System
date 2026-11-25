namespace ETOMS_API.DTOs.Team;

// Tham số lọc/tìm kiếm cho danh sách đội — truyền qua query string (?q=&gameId=&status=&sort=&page=&pageSize=)
// để URL chia sẻ/đánh dấu trang được và test được trực tiếp từng URL.
public class TeamSearchRequest
{
    public string? Q { get; set; }          // tìm theo tên hoặc thẻ đội
    public int? GameId { get; set; }         // lọc theo tựa game
    public string Status { get; set; } = "ACTIVE"; // ACTIVE | DISBANDED | ALL
    public string Sort { get; set; } = "newest";     // newest | name | members
    public int Page { get; set; } = 1;               // bắt đầu từ 1
    public int PageSize { get; set; } = 12;          // 1..48
}

public sealed record TeamListPage(IReadOnlyList<TeamListItemDto> Items, int Total, int Page, int PageSize, int TotalPages);

// Trận sắp tới / đang diễn ra của đội — lối vào trang trận và phòng chờ (MAT-07) từ trang đội.
public sealed record TeamMatchItemDto(int MatchId, int TournamentId, string TournamentName, string OpponentName,
    DateTime? ScheduledAt, string Status, string StatusLabel, bool LobbyOpen);
