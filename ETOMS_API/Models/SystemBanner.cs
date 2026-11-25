namespace ETOMS_API.Models;

// Banner quảng cáo / sự kiện ở đầu trang chủ (bảng có sẵn SystemBanner). Quản trị ở /admin/ads.html.
public partial class SystemBanner
{
    public int SystemBannerId { get; set; }
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public string ImageUrl { get; set; } = null!;
    public string? LinkUrl { get; set; }
    public string BannerType { get; set; } = null!;   // EVENT | GIVEAWAY (CK_SystemBanner_Type)
    public bool IsActive { get; set; }
    public int DisplayOrder { get; set; }              // >= 1
    public DateTime StartAt { get; set; }               // StartAt < EndAt
    public DateTime EndAt { get; set; }
    public int CreatedByAccountId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public virtual Account CreatedByAccount { get; set; } = null!;
}
