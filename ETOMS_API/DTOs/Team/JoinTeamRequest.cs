using System.ComponentModel.DataAnnotations;

namespace ETOMS_API.DTOs.Team;

// TEAM-05: nhập mã mời để gia nhập đội.
public class JoinTeamRequest
{
    [Required(ErrorMessage = "Vui lòng nhập mã mời.")]
    [StringLength(40, MinimumLength = 4, ErrorMessage = "Mã mời không hợp lệ.")]
    [Display(Name = "Mã mời")]
    public string Code { get; set; } = string.Empty;
}
