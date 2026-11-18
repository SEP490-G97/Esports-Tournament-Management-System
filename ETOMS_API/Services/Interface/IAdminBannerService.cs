using ETOMS_API.DTOs.Admin;

namespace ETOMS_API.Services.Interface;

public enum BannerError
{
    None,
    NotFound,
    InvalidTitle,
    InvalidDescription,
    InvalidImage,
    InvalidLink,
    InvalidType,
    InvalidPeriod,
    InvalidPosition,
    InvalidFilter,
    TooManyBanners,
    AlreadyInState,
    ReorderMismatch
}

/// <summary>OldImageUrl: ảnh cũ không còn banner nào dùng → controller xóa file.</summary>
public record BannerResult(BannerError Error, BannerItem? Item = null, string? OldImageUrl = null);

public record BannerListResult(BannerError Error, BannerListResponse? Data = null);

public record BannerReorderResult(BannerError Error, List<BannerItem>? Items = null);

public interface IAdminBannerService
{
    /// <summary>state: CURRENT (mặc định) | EXPIRED | ALL | LIVE | SCHEDULED | INACTIVE.</summary>
    Task<BannerListResult> ListAsync(string? state, CancellationToken ct = default);

    Task<BannerResult> CreateAsync(int actorId, BannerRequest request, CancellationToken ct = default);
    Task<BannerResult> UpdateAsync(int actorId, int id, BannerRequest request, CancellationToken ct = default);
    Task<BannerResult> SetActiveAsync(int actorId, int id, bool active, CancellationToken ct = default);
    Task<BannerResult> DeleteAsync(int actorId, int id, CancellationToken ct = default);
    Task<BannerReorderResult> ReorderAsync(int actorId, int[]? ids, CancellationToken ct = default);

    /// <summary>Banner đang hiển thị (IsActive, StartAt &lt;= now &lt; EndAt) theo DisplayOrder.</summary>
    Task<List<PublicBanner>> GetLiveAsync(CancellationToken ct = default);
}