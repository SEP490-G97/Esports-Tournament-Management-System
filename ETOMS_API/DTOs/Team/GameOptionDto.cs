namespace ETOMS_API.DTOs.Team;

// Mục chọn trong dropdown "Tựa game". Tạm lấy trong TeamService; khi ADM-03 (Vũ Đức Hải)
// làm xong GameService thì chuyển sang gọi service đó.
public class GameOptionDto
{
    public int GameId { get; set; }
    public string Title { get; set; } = string.Empty;
    // Quân số tối đa của một đội (chính thức + dự bị) — trang tạo đội giới hạn số người mời.
    public int MaxStarters { get; set; }
    public int MaxSubstitutes { get; set; }
}
