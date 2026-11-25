using ETOMS_API.Models;
using ETOMS_API.Repositories;
using ETOMS_API.Services.Interface;
using ETOMS_API.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ETOMS_API.Controllers;

// UC-42 — Ban tổ chức thêm / sửa / xóa nhà tài trợ của giải mình (bảng có sẵn TournamentSponsor).
// BR-SPN-01: nhà tài trợ thuộc đúng một giải, hạng theo giải đó (UQ_TournamentSponsor_Name chặn trùng tên trong giải).
// BR-SPN-02: hủy giải không xóa nhà tài trợ; Ban tổ chức chọn có hiện logo trên giải đã hủy (ShowWhenCancelled).
// Trang công khai đọc qua TournamentMarketingService / HomeApiController như trước.
[Route("api/tournaments/{tournamentId:int}/sponsors")]
public sealed class TournamentSponsorsApiController : ApiControllerBase
{
    public static readonly string[] Tiers = { "DIAMOND", "GOLD", "SILVER", "BRONZE", "PARTNER" };

    private readonly EtomsDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IWebHostEnvironment _env;

    public TournamentSponsorsApiController(EtomsDbContext db, ICurrentUser currentUser, IWebHostEnvironment env)
    {
        _db = db;
        _currentUser = currentUser;
        _env = env;
    }

    public sealed class SponsorForm
    {
        public string? Name { get; set; }
        public string? Tier { get; set; }
        public string? WebsiteUrl { get; set; }
        public string? ContactInfo { get; set; }
        public int? DisplayOrder { get; set; }
        public bool ShowWhenCancelled { get; set; } = true;
        public bool RemoveLogo { get; set; }
    }

    public static string TierLabel(string tier) => tier switch
    {
        "DIAMOND" => "Kim cương", "GOLD" => "Vàng", "SILVER" => "Bạc", "BRONZE" => "Đồng", _ => "Đối tác"
    };

    private async Task<(Tournament? T, IActionResult? Error)> LoadOwnedAsync(int tournamentId, CancellationToken ct)
    {
        if (!_currentUser.IsAuthenticated) return (null, Unauthorized());
        var t = await _db.Tournaments.FirstOrDefaultAsync(x => x.TournamentId == tournamentId, ct);
        if (t is null) return (null, FromResult(Result.Failure("Không tìm thấy giải đấu.", StatusCodes.Status404NotFound)));
        if (t.OrganizerAccountId != _currentUser.UserId)
            return (null, FromResult(Result.Failure("Chỉ ban tổ chức của giải mới quản lý được nhà tài trợ.", StatusCodes.Status403Forbidden)));
        return (t, null);
    }

    [HttpGet]
    public async Task<IActionResult> List(int tournamentId, CancellationToken ct)
    {
        var (t, error) = await LoadOwnedAsync(tournamentId, ct);
        if (error is not null) return error;
        var rows = await _db.TournamentSponsors.AsNoTracking()
            .Where(s => s.TournamentId == tournamentId)
            .OrderBy(s => s.Tier == "DIAMOND" ? 0 : s.Tier == "GOLD" ? 1 : s.Tier == "SILVER" ? 2 : s.Tier == "BRONZE" ? 3 : 4)
            .ThenBy(s => s.DisplayOrder)
            .Select(s => new
            {
                s.TournamentSponsorId, s.Name, s.LogoUrl, s.WebsiteUrl, s.ContactInfo, s.Tier,
                s.DisplayOrder, s.ShowWhenCancelled, s.CreatedAt
            }).ToListAsync(ct);
        return FromResult(Result.Success(new
        {
            TournamentId = t!.TournamentId,
            TournamentName = t.Name,
            TournamentStatus = StatusText.Tournament(t.Status),
            Sponsors = rows.Select(s => new
            {
                s.TournamentSponsorId, s.Name, s.LogoUrl, s.WebsiteUrl, s.ContactInfo, s.Tier, TierLabel = TierLabel(s.Tier),
                s.DisplayOrder, s.ShowWhenCancelled, s.CreatedAt
            })
        }));
    }

    [HttpPost]
    [RequestSizeLimit(3 * 1024 * 1024)]
    public async Task<IActionResult> Create(int tournamentId, [FromForm] SponsorForm form, IFormFile? logo, CancellationToken ct)
    {
        var (t, error) = await LoadOwnedAsync(tournamentId, ct);
        if (error is not null) return error;
        if (Validate(form) is { } bad) return bad;
        var name = form.Name!.Trim();
        if (await _db.TournamentSponsors.AnyAsync(s => s.TournamentId == tournamentId && s.Name == name, ct))
            return FromResult(Result.FieldFailure(nameof(form.Name), "Nhà tài trợ này đã có trong danh sách của giải."));   // MSG-SPN-001

        var upload = ImageUploader.Save(logo, _env, "sponsors");
        if (!upload.Ok) return FromResult(Result.FieldFailure("logo", upload.Error!));
        var order = form.DisplayOrder ?? (await _db.TournamentSponsors.Where(s => s.TournamentId == tournamentId)
            .Select(s => (int?)s.DisplayOrder).MaxAsync(ct) ?? 0) + 1;
        var sponsor = new TournamentSponsor
        {
            TournamentId = tournamentId,
            Name = name,
            Tier = form.Tier!.Trim().ToUpperInvariant(),
            LogoUrl = upload.Url,
            WebsiteUrl = Clean(form.WebsiteUrl),
            ContactInfo = Clean(form.ContactInfo),
            DisplayOrder = order,
            ShowWhenCancelled = form.ShowWhenCancelled,
            CreatedByAccountId = _currentUser.UserId!.Value,
            CreatedAt = DateTime.UtcNow
        };
        _db.TournamentSponsors.Add(sponsor);
        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateException)
        {
            ImageUploader.Delete(upload.Url, _env);
            return FromResult(Result.FieldFailure(nameof(form.Name), "Nhà tài trợ này đã có trong danh sách của giải."));
        }
        return FromResult(Result.Success(new { sponsor.TournamentSponsorId }, $"Đã thêm nhà tài trợ {name} vào giải {t!.Name}."));
    }

    [HttpPut("{sponsorId:int}")]
    [RequestSizeLimit(3 * 1024 * 1024)]
    public async Task<IActionResult> Update(int tournamentId, int sponsorId, [FromForm] SponsorForm form, IFormFile? logo, CancellationToken ct)
    {
        var (_, error) = await LoadOwnedAsync(tournamentId, ct);
        if (error is not null) return error;
        var sponsor = await _db.TournamentSponsors.FirstOrDefaultAsync(s => s.TournamentSponsorId == sponsorId && s.TournamentId == tournamentId, ct);
        if (sponsor is null) return FromResult(Result.Failure("Không tìm thấy nhà tài trợ trong giải này.", StatusCodes.Status404NotFound));
        if (Validate(form) is { } bad) return bad;
        var name = form.Name!.Trim();
        if (await _db.TournamentSponsors.AnyAsync(s => s.TournamentId == tournamentId && s.Name == name && s.TournamentSponsorId != sponsorId, ct))
            return FromResult(Result.FieldFailure(nameof(form.Name), "Nhà tài trợ này đã có trong danh sách của giải."));

        var upload = ImageUploader.Save(logo, _env, "sponsors");
        if (!upload.Ok) return FromResult(Result.FieldFailure("logo", upload.Error!));
        var oldLogo = sponsor.LogoUrl;
        sponsor.Name = name;
        sponsor.Tier = form.Tier!.Trim().ToUpperInvariant();
        sponsor.WebsiteUrl = Clean(form.WebsiteUrl);
        sponsor.ContactInfo = Clean(form.ContactInfo);
        if (form.DisplayOrder is int order) sponsor.DisplayOrder = order;
        sponsor.ShowWhenCancelled = form.ShowWhenCancelled;
        if (upload.Url is not null) sponsor.LogoUrl = upload.Url;
        else if (form.RemoveLogo) sponsor.LogoUrl = null;
        await _db.SaveChangesAsync(ct);
        if (oldLogo != sponsor.LogoUrl) ImageUploader.Delete(oldLogo, _env);
        return FromResult(Result.Success("Đã lưu nhà tài trợ."));
    }

    [HttpDelete("{sponsorId:int}")]
    public async Task<IActionResult> Delete(int tournamentId, int sponsorId, CancellationToken ct)
    {
        var (_, error) = await LoadOwnedAsync(tournamentId, ct);
        if (error is not null) return error;
        var sponsor = await _db.TournamentSponsors.FirstOrDefaultAsync(s => s.TournamentSponsorId == sponsorId && s.TournamentId == tournamentId, ct);
        if (sponsor is null) return FromResult(Result.Failure("Không tìm thấy nhà tài trợ trong giải này.", StatusCodes.Status404NotFound));
        _db.TournamentSponsors.Remove(sponsor);
        await _db.SaveChangesAsync(ct);
        ImageUploader.Delete(sponsor.LogoUrl, _env);
        return FromResult(Result.Success($"Đã gỡ nhà tài trợ {sponsor.Name} khỏi giải."));
    }

    private IActionResult? Validate(SponsorForm f)
    {
        var name = (f.Name ?? "").Trim();
        if (name.Length == 0) return FromResult(Result.FieldFailure(nameof(f.Name), "Vui lòng nhập tên nhà tài trợ."));
        if (name.Length > 200) return FromResult(Result.FieldFailure(nameof(f.Name), "Tên nhà tài trợ tối đa 200 ký tự."));
        if (!Tiers.Contains((f.Tier ?? "").Trim().ToUpperInvariant()))
            return FromResult(Result.FieldFailure(nameof(f.Tier), "Hạng tài trợ phải là Kim cương, Vàng, Bạc, Đồng hoặc Đối tác."));
        var site = Clean(f.WebsiteUrl);
        if (site is not null && (site.Length > 500 || !Uri.TryCreate(site, UriKind.Absolute, out var uri) || (uri.Scheme != "http" && uri.Scheme != "https")))
            return FromResult(Result.FieldFailure(nameof(f.WebsiteUrl), "Trang web phải là địa chỉ http(s) hợp lệ, tối đa 500 ký tự."));
        if ((Clean(f.ContactInfo)?.Length ?? 0) > 300)
            return FromResult(Result.FieldFailure(nameof(f.ContactInfo), "Thông tin liên hệ tối đa 300 ký tự."));
        if (f.DisplayOrder is int o && (o < 1 || o > 999))
            return FromResult(Result.FieldFailure(nameof(f.DisplayOrder), "Thứ tự hiển thị từ 1 đến 999."));
        return null;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
