using ETOMS_API.DTOs.Admin;
using ETOMS_API.Models;
using ETOMS_API.Repositories;
using ETOMS_API.Services.Interface;
using ETOMS_API.Utils;
using Microsoft.EntityFrameworkCore;

namespace ETOMS_API.Services.Impl;

public class AdminBannerService : IAdminBannerService
{
    public const string StateLive = "LIVE";
    public const string StateScheduled = "SCHEDULED";
    public const string StateInactive = "INACTIVE";
    public const string StateExpired = "EXPIRED";

    private readonly EtomsDbContext _db;

    public AdminBannerService(EtomsDbContext db) => _db = db;

    private IQueryable<SystemBanner> Banners => _db.Set<SystemBanner>();

    // ---------------------------------------------------------------- danh sách

    public async Task<BannerListResult> ListAsync(string? state, CancellationToken ct = default)
    {
        var f = (state ?? "CURRENT").Trim().ToUpperInvariant();
        if (f is not ("CURRENT" or "EXPIRED" or "ALL" or StateLive or StateScheduled or StateInactive))
            return new BannerListResult(BannerError.InvalidFilter);

        var now = DateTime.UtcNow;
        var all = await Banners.AsNoTracking().ToListAsync(ct);

        var counts = new BannerCounts(
            Current: all.Count(b => b.EndAt > now),
            Live: all.Count(b => StateOf(b, now) == StateLive),
            Scheduled: all.Count(b => StateOf(b, now) == StateScheduled),
            Inactive: all.Count(b => StateOf(b, now) == StateInactive),
            Expired: all.Count(b => StateOf(b, now) == StateExpired));

        IEnumerable<SystemBanner> filtered = f switch
        {
            "CURRENT" => all.Where(b => b.EndAt > now).OrderBy(b => b.DisplayOrder).ThenBy(b => b.SystemBannerId),
            "EXPIRED" => all.Where(b => b.EndAt <= now).OrderByDescending(b => b.EndAt).ThenByDescending(b => b.SystemBannerId),
            "ALL" => all.OrderBy(b => b.EndAt <= now).ThenBy(b => b.DisplayOrder).ThenBy(b => b.SystemBannerId),
            _ => all.Where(b => StateOf(b, now) == f).OrderBy(b => b.DisplayOrder).ThenBy(b => b.SystemBannerId)
        };

        return new BannerListResult(BannerError.None,
            new BannerListResponse(filtered.Select(b => ToItem(b, now)).ToList(), counts, AdminConstants.MaxCurrentBanners));
    }

    // ---------------------------------------------------------------- tạo

    public async Task<BannerResult> CreateAsync(int actorId, BannerRequest request, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var v = Validate(request, null, now, requireFutureEnd: true);
        if (v.Error != BannerError.None) return new BannerResult(v.Error);

        if (request.Position is < 1) return new BannerResult(BannerError.InvalidPosition);

        var current = await Banners.Where(b => b.EndAt > now)
            .OrderBy(b => b.DisplayOrder).ThenBy(b => b.SystemBannerId).ToListAsync(ct);
        if (current.Count >= AdminConstants.MaxCurrentBanners) return new BannerResult(BannerError.TooManyBanners);

        var entity = new SystemBanner
        {
            Title = v.Title!,
            Description = v.Description,
            ImageUrl = v.ImageUrl!,
            LinkUrl = v.LinkUrl,
            BannerType = v.Type!,
            IsActive = request.IsActive ?? true,
            DisplayOrder = current.Count + 1,
            StartAt = v.Start,
            EndAt = v.End,
            CreatedByAccountId = actorId,
            CreatedAt = now,
            UpdatedAt = now
        };

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        _db.Set<SystemBanner>().Add(entity);
        await _db.SaveChangesAsync(ct);

        // Đặt vào vị trí yêu cầu (mặc định cuối danh sách) rồi đánh số lại 1..n.
        var list = current.ToList();
        var index = request.Position.HasValue ? Math.Min(request.Position.Value, list.Count + 1) - 1 : list.Count;
        list.Insert(index, entity);
        Renumber(list, now, except: entity);

        AdminAudit.Add(_db, actorId, AdminAuditActions.BannerCreate, "SYSTEM_BANNER", entity.SystemBannerId,
            null, Snapshot(entity), entity.Title);
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return new BannerResult(BannerError.None, ToItem(entity, now));
    }

    // ---------------------------------------------------------------- sửa

    public async Task<BannerResult> UpdateAsync(int actorId, int id, BannerRequest request, CancellationToken ct = default)
    {
        var entity = await Banners.FirstOrDefaultAsync(b => b.SystemBannerId == id, ct);
        if (entity == null) return new BannerResult(BannerError.NotFound);

        var now = DateTime.UtcNow;
        var v = Validate(request, entity, now, requireFutureEnd: false);
        if (v.Error != BannerError.None) return new BannerResult(v.Error);
        if (request.Position is < 1) return new BannerResult(BannerError.InvalidPosition);

        var before = Snapshot(entity);
        var oldImage = entity.ImageUrl;
        bool wasCurrent = entity.EndAt > now;

        entity.Title = v.Title!;
        entity.Description = v.Description;
        entity.ImageUrl = v.ImageUrl!;
        entity.LinkUrl = v.LinkUrl;
        entity.BannerType = v.Type!;
        entity.StartAt = v.Start;
        entity.EndAt = v.End;
        if (request.IsActive.HasValue) entity.IsActive = request.IsActive.Value;
        entity.UpdatedAt = now;

        bool isCurrent = entity.EndAt > now;

        var others = await Banners.Where(b => b.SystemBannerId != id && b.EndAt > now)
            .OrderBy(b => b.DisplayOrder).ThenBy(b => b.SystemBannerId).ToListAsync(ct);

        // Banner hết hạn được gia hạn quay lại nhóm đang áp dụng → kiểm tra trần.
        if (isCurrent && !wasCurrent && others.Count >= AdminConstants.MaxCurrentBanners)
            return new BannerResult(BannerError.TooManyBanners);

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        if (isCurrent)
        {
            int index;
            if (request.Position.HasValue) index = Math.Min(request.Position.Value, others.Count + 1) - 1;
            else if (wasCurrent) index = Math.Clamp(Math.Max(before.DisplayOrder, 1) - 1, 0, others.Count);
            else index = others.Count; // vừa được gia hạn: đặt cuối
            var list = others.ToList();
            list.Insert(index, entity);
            Renumber(list, now, except: entity);
        }
        else
        {
            // Vừa hết hạn: các banner còn lại đánh số lại liền mạch.
            Renumber(others, now, except: null);
        }

        AdminAudit.Add(_db, actorId, AdminAuditActions.BannerUpdate, "SYSTEM_BANNER", id, before, Snapshot(entity), entity.Title);
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        string? orphan = null;
        if (!string.Equals(oldImage, entity.ImageUrl, StringComparison.Ordinal) &&
            !await Banners.AnyAsync(b => b.ImageUrl == oldImage, ct))
            orphan = oldImage;

        return new BannerResult(BannerError.None, ToItem(entity, now), orphan);
    }

    // ---------------------------------------------------------------- bật / tắt

    public async Task<BannerResult> SetActiveAsync(int actorId, int id, bool active, CancellationToken ct = default)
    {
        var entity = await Banners.FirstOrDefaultAsync(b => b.SystemBannerId == id, ct);
        if (entity == null) return new BannerResult(BannerError.NotFound);
        if (entity.IsActive == active) return new BannerResult(BannerError.AlreadyInState);

        var before = Snapshot(entity);
        var now = DateTime.UtcNow;
        entity.IsActive = active;
        entity.UpdatedAt = now;

        AdminAudit.Add(_db, actorId, AdminAuditActions.BannerUpdate, "SYSTEM_BANNER", id, before, Snapshot(entity),
            active ? "Bật banner" : "Tắt banner");
        await _db.SaveChangesAsync(ct);
        return new BannerResult(BannerError.None, ToItem(entity, now));
    }

    // ---------------------------------------------------------------- xóa

    public async Task<BannerResult> DeleteAsync(int actorId, int id, CancellationToken ct = default)
    {
        var entity = await Banners.FirstOrDefaultAsync(b => b.SystemBannerId == id, ct);
        if (entity == null) return new BannerResult(BannerError.NotFound);

        var now = DateTime.UtcNow;
        var before = Snapshot(entity);
        var image = entity.ImageUrl;
        var item = ToItem(entity, now);

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        _db.Set<SystemBanner>().Remove(entity);
        await _db.SaveChangesAsync(ct);

        var rest = await Banners.Where(b => b.EndAt > now)
            .OrderBy(b => b.DisplayOrder).ThenBy(b => b.SystemBannerId).ToListAsync(ct);
        Renumber(rest, now, except: null);

        AdminAudit.Add(_db, actorId, AdminAuditActions.BannerDelete, "SYSTEM_BANNER", id, before, null, before.Title);
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        var orphan = await Banners.AnyAsync(b => b.ImageUrl == image, ct) ? null : image;
        return new BannerResult(BannerError.None, item, orphan);
    }

    // ---------------------------------------------------------------- sắp xếp

    public async Task<BannerReorderResult> ReorderAsync(int actorId, int[]? ids, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var current = await Banners.Where(b => b.EndAt > now)
            .OrderBy(b => b.DisplayOrder).ThenBy(b => b.SystemBannerId).ToListAsync(ct);

        var requested = ids ?? Array.Empty<int>();
        if (requested.Length != current.Count || requested.Distinct().Count() != requested.Length ||
            !current.Select(b => b.SystemBannerId).OrderBy(x => x).SequenceEqual(requested.OrderBy(x => x)))
            return new BannerReorderResult(BannerError.ReorderMismatch);

        var before = current.Select(b => b.SystemBannerId).ToArray();
        if (before.SequenceEqual(requested))
            return new BannerReorderResult(BannerError.None, current.Select(b => ToItem(b, now)).ToList());

        var byId = current.ToDictionary(b => b.SystemBannerId);
        var ordered = requested.Select(i => byId[i]).ToList();
        Renumber(ordered, now, except: null);

        AdminAudit.Add(_db, actorId, AdminAuditActions.BannerReorder, "SYSTEM_BANNER", null,
            new { ids = before }, new { ids = requested }, "Sắp xếp lại banner");
        await _db.SaveChangesAsync(ct);

        return new BannerReorderResult(BannerError.None, ordered.Select(b => ToItem(b, now)).ToList());
    }

    // ---------------------------------------------------------------- công khai

    public async Task<List<PublicBanner>> GetLiveAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        return await Banners.AsNoTracking()
            .Where(b => b.IsActive && b.StartAt <= now && b.EndAt > now)
            .OrderBy(b => b.DisplayOrder).ThenBy(b => b.SystemBannerId)
            .Take(20)
            .Select(b => new PublicBanner(b.SystemBannerId, b.Title, b.Description, b.ImageUrl, b.LinkUrl, b.BannerType))
            .ToListAsync(ct);
    }

    // ---------------------------------------------------------------- nội bộ

    internal static string StateOf(SystemBanner b, DateTime now)
    {
        if (b.EndAt <= now) return StateExpired;
        if (!b.IsActive) return StateInactive;
        return b.StartAt > now ? StateScheduled : StateLive;
    }

    private static BannerItem ToItem(SystemBanner b, DateTime now) => new(
        b.SystemBannerId, b.Title, b.Description, b.ImageUrl, b.LinkUrl, b.BannerType, b.IsActive, b.DisplayOrder,
        AsUtc(b.StartAt), AsUtc(b.EndAt), StateOf(b, now), AsUtc(b.UpdatedAt));

    private static DateTime AsUtc(DateTime d) => DateTime.SpecifyKind(d, DateTimeKind.Utc);

    private static DateTime ToDb(DateTimeOffset d) => DateTime.SpecifyKind(d.UtcDateTime, DateTimeKind.Unspecified);

    /// <summary>Gán DisplayOrder 1..n theo thứ tự danh sách; chỉ chạm vào dòng thực sự đổi (except: dòng đã được theo dõi riêng).</summary>
    private static void Renumber(List<SystemBanner> list, DateTime now, SystemBanner? except)
    {
        for (int i = 0; i < list.Count; i++)
        {
            var b = list[i];
            if (b.DisplayOrder == i + 1) continue;
            b.DisplayOrder = i + 1;
            if (!ReferenceEquals(b, except)) b.UpdatedAt = now;
        }
    }

    private static BannerSnapshot Snapshot(SystemBanner b) => new(
        b.Title, b.Description, b.ImageUrl, b.LinkUrl, b.BannerType, b.IsActive, b.DisplayOrder, b.StartAt, b.EndAt);

    private record BannerSnapshot(string Title, string? Description, string ImageUrl, string? LinkUrl, string BannerType,
        bool IsActive, int DisplayOrder, DateTime StartAt, DateTime EndAt);

    private record Validated(BannerError Error, string? Title = null, string? Description = null, string? ImageUrl = null,
        string? LinkUrl = null, string? Type = null, DateTime Start = default, DateTime End = default);

    private static Validated Validate(BannerRequest r, SystemBanner? existing, DateTime now, bool requireFutureEnd)
    {
        var title = (r.Title ?? "").Trim();
        if (title.Length < 3 || title.Length > 200) return new Validated(BannerError.InvalidTitle);

        var desc = (r.Description ?? "").Trim();
        if (desc.Length > 500) return new Validated(BannerError.InvalidDescription);

        var image = (r.ImageUrl ?? "").Trim();
        if (image.Length == 0 || !LinkRules.IsValidOptional(image)) return new Validated(BannerError.InvalidImage);
        image = LinkRules.Normalize(image)!;

        if (!LinkRules.IsValidOptional(r.LinkUrl)) return new Validated(BannerError.InvalidLink);
        var link = LinkRules.Normalize(r.LinkUrl);

        var type = (r.BannerType ?? "").Trim().ToUpperInvariant();
        if (!AdminConstants.BannerTypes.Contains(type)) return new Validated(BannerError.InvalidType);

        DateTime start = r.StartAt.HasValue ? ToDb(r.StartAt.Value) : existing?.StartAt ?? now;
        if (r.EndAt == null && existing == null) return new Validated(BannerError.InvalidPeriod);
        DateTime end = r.EndAt.HasValue ? ToDb(r.EndAt.Value) : existing!.EndAt;

        if (start >= end) return new Validated(BannerError.InvalidPeriod);
        if (requireFutureEnd && end <= now) return new Validated(BannerError.InvalidPeriod);

        return new Validated(BannerError.None, title, desc.Length == 0 ? null : desc, image, link, type, start, end);
    }
}