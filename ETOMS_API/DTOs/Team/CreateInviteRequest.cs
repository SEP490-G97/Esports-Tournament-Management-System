using System.ComponentModel.DataAnnotations;

namespace ETOMS_API.DTOs.Team;

// Tùy chọn khi tạo/làm mới mã mời (v2): chỉ còn thời hạn. Để trống = vô thời hạn.
public class CreateInviteRequest
{
    [Range(1, 8760, ErrorMessage = "Thời hạn phải từ 1 đến 8760 giờ (1 năm).")]
    [Display(Name = "Thời hạn (giờ)")]
    public int? ExpiresHours { get; set; }
}
