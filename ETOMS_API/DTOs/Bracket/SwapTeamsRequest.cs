using System.ComponentModel.DataAnnotations;

namespace ETOMS_API.DTOs.Bracket;

// Đổi chỗ 2 đội trong nhánh đã sinh (kéo thả trên sơ đồ) — chỉ khi nhánh chưa khóa.
public class SwapTeamsRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "Thiếu đội thứ nhất.")]
    public int TeamAId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Thiếu đội thứ hai.")]
    public int TeamBId { get; set; }
}
