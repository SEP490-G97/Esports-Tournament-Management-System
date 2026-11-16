using System.ComponentModel.DataAnnotations;

namespace ETOMS_API.DTOs.Bracket;

// BRK-01/BRK-02: tham số sinh nhánh đấu — chọn cách xếp hạt giống trước khi dựng cây.
public class GenerateBracketRequest
{
    [Required(ErrorMessage = "Vui lòng chọn cách xếp hạt giống.")]
    public string SeedingMethod { get; set; } = "BY_RANKING"; // BY_RANKING | RANDOM_DRAW | MANUAL

    // Chỉ dùng khi SeedingMethod = MANUAL: danh sách TeamId theo đúng thứ tự hạt giống mong muốn.
    public List<int>? ManualOrder { get; set; }
}
