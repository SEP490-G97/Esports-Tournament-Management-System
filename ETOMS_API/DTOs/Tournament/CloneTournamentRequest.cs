using System.ComponentModel.DataAnnotations;

namespace ETOMS_API.DTOs.Tournament;

// TOUR-14: Tên và lịch mới của giải nhân bản, vì lịch của giải cũ đã qua.
public class CloneTournamentRequest
{
    [Required(ErrorMessage = "Vui lòng nhập đầy đủ thông tin bắt buộc.")]
    [StringLength(200, ErrorMessage = "Tên giải tối đa 200 ký tự.")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập đầy đủ thông tin bắt buộc.")]
    public DateTime? RegOpenAt { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập đầy đủ thông tin bắt buộc.")]
    public DateTime? RegCloseAt { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập đầy đủ thông tin bắt buộc.")]
    public DateTime? StartAt { get; set; }
}
