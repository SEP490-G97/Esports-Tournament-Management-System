namespace ETOMS_API.DTOs.Team;

// Tham số lọc/tìm kiếm cho danh sách đội — truyền qua query string (?q=&gameId=&status=&sort=)
// để URL chia sẻ/đánh dấu trang được và test được trực tiếp từng URL.
public class TeamSearchRequest
{
    public string? Q { get; set; }          // tìm theo tên hoặc thẻ đội
    public int? GameId { get; set; }         // lọc theo tựa game
    public string Status { get; set; } = "ACTIVE"; // ACTIVE | DISBANDED | ALL
    public string Sort { get; set; } = "newest";     // newest | name | members
}
