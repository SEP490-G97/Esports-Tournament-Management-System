namespace ETOMS_API.DTOs.Team;

// Mục chọn trong dropdown "Tựa game". Tạm lấy trong TeamService; khi ADM-03 (Vũ Đức Hải)
// làm xong GameService thì chuyển sang gọi service đó.
public class GameOptionDto
{
    public int GameId { get; set; }
    public string Title { get; set; } = string.Empty;
}
