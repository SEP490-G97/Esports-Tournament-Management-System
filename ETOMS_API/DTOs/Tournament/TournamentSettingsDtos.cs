using System.ComponentModel.DataAnnotations;

namespace ETOMS_API.DTOs.Tournament;

// TOUR-10: sửa thông tin giải theo trạng thái.
public class TournamentSettingsBoardDto
{
    public int TournamentId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string StatusLabel { get; set; } = string.Empty;
    public string StatusClass { get; set; } = string.Empty;
    public bool IsBracketLocked { get; set; }
    public int ApprovedCount { get; set; }
    public List<GameOptionDto> Games { get; set; } = new();
    public TournamentSettingsValues Values { get; set; } = new();
    // Nhóm trường: INFO, SETUP, TIMELINE, FORMAT. Có lý do = đang khóa.
    public Dictionary<string, string?> LockReasons { get; set; } = new();
    // Mốc thời gian đã qua của giải đã xuất bản, không đổi được nữa.
    public List<string> PastMilestones { get; set; } = new();
}

public class GameOptionDto
{
    public int GameId { get; set; }
    public string Title { get; set; } = string.Empty;
}

public class TournamentSettingsValues
{
    // Thông tin chung (INFO)
    [MaxLength(20000, ErrorMessage = "Mô tả tối đa 20000 ký tự.")]
    public string? Description { get; set; }
    [MaxLength(500, ErrorMessage = "Đường dẫn ảnh bìa tối đa 500 ký tự.")]
    public string? BannerUrl { get; set; }
    [MaxLength(200, ErrorMessage = "Đơn vị tổ chức tối đa 200 ký tự.")]
    public string? OrganizingUnit { get; set; }
    [MaxLength(100, ErrorMessage = "Khu vực tối đa 100 ký tự.")]
    public string? Region { get; set; }
    [MaxLength(300, ErrorMessage = "Thông tin liên hệ tối đa 300 ký tự.")]
    public string? ContactInfo { get; set; }
    public bool IsOnline { get; set; }
    [MaxLength(300, ErrorMessage = "Địa điểm tối đa 300 ký tự.")]
    public string? Venue { get; set; }

    // Thiết lập đội đã đăng ký theo (SETUP)
    [Required(ErrorMessage = "Vui lòng nhập tên giải.")]
    [MaxLength(200, ErrorMessage = "Tên giải tối đa 200 ký tự.")]
    public string Name { get; set; } = string.Empty;
    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn tựa game.")]
    public int GameId { get; set; }
    [Range(typeof(decimal), "0", "1000000000", ErrorMessage = "Lệ phí từ 0 đến 1.000.000.000 đồng.")]
    public decimal EntryFee { get; set; }
    [MaxLength(300, ErrorMessage = "Thông tin tài khoản tối đa 300 ký tự.")]
    public string? BankAccountInfo { get; set; }
    [MaxLength(20000, ErrorMessage = "Cơ cấu giải thưởng tối đa 20000 ký tự.")]
    public string? PrizeStructure { get; set; }
    public bool RequirePaymentProof { get; set; }
    public bool RequireInGameName { get; set; }
    [Range(1, 100, ErrorMessage = "Số người chính thức từ 1 đến 100.")]
    public int StarterCount { get; set; }
    [Range(0, 100, ErrorMessage = "Số dự bị từ 0 đến 100.")]
    public int MaxSubstitutes { get; set; }

    // Thời gian và quy mô (TIMELINE)
    public DateTime RegOpenAt { get; set; }
    public DateTime RegCloseAt { get; set; }
    public DateTime StartAt { get; set; }
    public DateTime? EndAt { get; set; }
    [Range(2, 1024, ErrorMessage = "Số đội tối đa từ 2 đến 1024.")]
    public int MaxTeams { get; set; }

    // Cấu hình thể thức (FORMAT)
    [Required(ErrorMessage = "Vui lòng chọn thể thức.")]
    public string Format { get; set; } = string.Empty;
    public int? GroupCount { get; set; }
    public int? AdvancePerGroup { get; set; }
    public bool HasThirdPlaceMatch { get; set; }
    [MaxLength(200, ErrorMessage = "Cấu hình số ván tối đa 200 ký tự.")]
    public string? BestOfPerRound { get; set; }
}
