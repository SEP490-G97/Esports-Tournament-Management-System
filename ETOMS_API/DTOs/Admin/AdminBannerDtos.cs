namespace ETOMS_API.DTOs.Admin;

// ------------------------------------------------------------------ Banner tổng (ADM-08)

public class BannerRequest
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public string? LinkUrl { get; set; }
    /// <summary>EVENT / GIVEAWAY.</summary>
    public string? BannerType { get; set; }
    /// <summary>Bỏ trống khi tạo = bắt đầu ngay.</summary>
    public DateTimeOffset? StartAt { get; set; }
    public DateTimeOffset? EndAt { get; set; }
    /// <summary>Vị trí 1..n trong nhóm "đang áp dụng". Bỏ trống = giữ nguyên (sửa) / thêm cuối (tạo).</summary>
    public int? Position { get; set; }
    /// <summary>Mặc định true khi tạo.</summary>
    public bool? IsActive { get; set; }
}

public class ReorderBannersRequest
{
    /// <summary>Toàn bộ Id banner đang áp dụng, theo thứ tự mới.</summary>
    public int[]? Ids { get; set; }
}

public record BannerItem(
    int Id,
    string Title,
    string? Description,
    string ImageUrl,
    string? LinkUrl,
    string BannerType,
    bool IsActive,
    int DisplayOrder,
    DateTime StartAt,
    DateTime EndAt,
    /// <summary>LIVE / SCHEDULED / INACTIVE / EXPIRED (tính tại thời điểm gọi).</summary>
    string State,
    DateTime UpdatedAt);

public record BannerCounts(int Current, int Live, int Scheduled, int Inactive, int Expired);

public record BannerListResponse(List<BannerItem> Items, BannerCounts Counts, int MaxCurrent);

/// <summary>Banner hiển thị công khai trên trang chủ.</summary>
public record PublicBanner(int Id, string Title, string? Description, string ImageUrl, string? LinkUrl, string BannerType);
