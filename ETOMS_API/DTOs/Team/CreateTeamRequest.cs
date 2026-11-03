using System.ComponentModel.DataAnnotations;

namespace ETOMS_API.DTOs.Team;

// Dữ liệu người dùng nhập khi tạo đội (v2). Validation cơ bản bằng DataAnnotations.
// Validation nghiệp vụ (tên/tag trùng, game tồn tại) nằm trong TeamService.
public class CreateTeamRequest
{
    [Required(ErrorMessage = "Vui lòng nhập tên đội.")]
    [StringLength(200, MinimumLength = 2, ErrorMessage = "Tên đội phải từ 2 đến 200 ký tự.")]
    [Display(Name = "Tên đội")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập thẻ đội (Team Tag).")]
    [StringLength(20, MinimumLength = 2, ErrorMessage = "Thẻ đội phải từ 2 đến 20 ký tự.")]
    [RegularExpression(@"^[A-Za-z0-9]+$", ErrorMessage = "Thẻ đội chỉ gồm chữ cái không dấu và chữ số, không khoảng trắng.")]
    [Display(Name = "Thẻ đội")]
    public string Tag { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng chọn tựa game chủ đạo.")]
    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn tựa game chủ đạo.")]
    [Display(Name = "Tựa game chủ đạo")]
    public int GameId { get; set; }

    // Đường dẫn logo sau khi upload thành công (Controller gán).
    public string? LogoUrl { get; set; }
}
