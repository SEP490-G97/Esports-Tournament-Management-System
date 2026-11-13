using System.Security;
using System.Text;
using ETOMS_API.DTOs.Marketing;
using ETOMS_API.Services.Interface;
using Microsoft.AspNetCore.Mvc;
using QRCoder;

namespace ETOMS_API.Controllers;

[Route("api/marketing")]
public sealed class TournamentMarketingController : ApiControllerBase
{
    private readonly ITournamentMarketingService _marketingService;
    private readonly ICurrentUser _currentUser;

    public TournamentMarketingController(
        ITournamentMarketingService marketingService,
        ICurrentUser currentUser)
    {
        _marketingService = marketingService;
        _currentUser = currentUser;
    }

    [HttpGet("tournaments/{tournamentId:int}")]
    public async Task<IActionResult> GetLanding(int tournamentId, CancellationToken cancellationToken)
        => FromResult(await _marketingService.GetLandingAsync(tournamentId, cancellationToken));

    [HttpGet("mine/tournaments")]
    public async Task<IActionResult> GetMyTournaments(CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not int accountId) return Unauthorized();
        return FromResult(await _marketingService.GetOrganizerTournamentsAsync(accountId, cancellationToken));
    }

    [HttpGet("tournaments/{tournamentId:int}/affiliate-links")]
    public async Task<IActionResult> GetAffiliateLinks(int tournamentId, CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not int accountId) return Unauthorized();
        return FromResult(await _marketingService.GetPromotionLinksAsync(
            tournamentId, accountId, GetPublicBaseUri(), cancellationToken));
    }

    [HttpPost("tournaments/{tournamentId:int}/affiliate-links")]
    public async Task<IActionResult> CreateAffiliateLink(
        int tournamentId,
        [FromBody] CreatePromotionLinkRequest request,
        CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not int accountId) return Unauthorized();
        var result = await _marketingService.CreatePromotionLinkAsync(
            tournamentId, accountId, request, GetPublicBaseUri(), cancellationToken);
        return result.IsSuccess
            ? Created(result.Data!.ShareUrl, new { message = result.Message, data = result.Data })
            : FromResult(result);
    }

    [HttpPost("affiliate-links/{code}/visit")]
    public async Task<IActionResult> TrackAffiliateVisit(string code, CancellationToken cancellationToken)
    {
        var key = $"affiliate-visit:{code.Trim().ToUpperInvariant()}";
        if (HttpContext.Session.GetString(key) is not null)
        {
            return Ok(new { message = "Lượt truy cập đã được ghi nhận trong phiên này." });
        }

        var result = await _marketingService.TrackVisitAsync(code, cancellationToken);
        if (result.IsSuccess) HttpContext.Session.SetString(key, "1");
        return FromResult(result);
    }

    [HttpGet("tournaments/{tournamentId:int}/qr.svg")]
    public async Task<IActionResult> GetRegistrationQr(
        int tournamentId,
        [FromQuery] string? code,
        CancellationToken cancellationToken)
    {
        var result = await _marketingService.GetRegistrationUrlAsync(
            tournamentId, code, GetPublicBaseUri(), cancellationToken);
        if (!result.IsSuccess) return FromResult(result);

        var svg = CreateQrSvg(result.Data!);
        return Content(svg, "image/svg+xml; charset=utf-8");
    }

    [HttpGet("tournaments/{tournamentId:int}/materials/{format}.svg")]
    public async Task<IActionResult> GetPromotionMaterial(
        int tournamentId,
        string format,
        [FromQuery] string? code,
        CancellationToken cancellationToken)
    {
        var landingResult = await _marketingService.GetLandingAsync(tournamentId, cancellationToken);
        if (!landingResult.IsSuccess) return FromResult(landingResult);

        var urlResult = await _marketingService.GetRegistrationUrlAsync(
            tournamentId, code, GetPublicBaseUri(), cancellationToken);
        if (!urlResult.IsSuccess) return FromResult(urlResult);

        var (width, height) = format.ToLowerInvariant() switch
        {
            "square" => (1080, 1080),
            "story" => (1080, 1920),
            "landscape" => (1200, 630),
            "logo" => (512, 512),
            "avatar-frame" => (1080, 1080),
            _ => (0, 0)
        };
        if (width == 0)
        {
            return BadRequest(new { message = "Kích thước ảnh không được hỗ trợ." });
        }

        var svg = format.ToLowerInvariant() switch
        {
            "logo" => CreateTournamentLogoSvg(landingResult.Data!, width, height),
            "avatar-frame" => CreateAvatarFrameSvg(landingResult.Data!, width, height),
            _ => CreatePromotionSvg(landingResult.Data!, urlResult.Data!, width, height)
        };
        return File(Encoding.UTF8.GetBytes(svg), "image/svg+xml; charset=utf-8",
            $"etoms-{format.ToLowerInvariant()}-{tournamentId}.svg");
    }

    private Uri GetPublicBaseUri()
        => new($"{Request.Scheme}://{Request.Host}{Request.PathBase}/");

    private static string CreateQrSvg(string url)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(url, QRCodeGenerator.ECCLevel.Q);
        return new SvgQRCode(data).GetGraphic(6, "#101527", "#ffffff", true);
    }

    private static string CreatePromotionSvg(
        TournamentLandingResponse landing,
        string registrationUrl,
        int width,
        int height)
    {
        var qr = Convert.ToBase64String(Encoding.UTF8.GetBytes(CreateQrSvg(registrationUrl)));
        var title = SecurityElement.Escape(landing.Name) ?? string.Empty;
        var game = SecurityElement.Escape(landing.GameName) ?? string.Empty;
        var action = SecurityElement.Escape(registrationUrl) ?? string.Empty;
        var compact = height < 800;
        var qrSize = compact ? 210 : 270;
        var qrX = width - qrSize - 72;
        var qrY = height - qrSize - 78;
        var titleSize = compact ? 58 : 78;
        var titleY = compact ? height / 2 - 18 : height / 2 - 70;
        return $"""
            <svg xmlns="http://www.w3.org/2000/svg" width="{width}" height="{height}" viewBox="0 0 {width} {height}">
              <defs>
                <linearGradient id="background" x1="0" y1="0" x2="1" y2="1">
                  <stop offset="0" stop-color="#111827"/>
                  <stop offset="1" stop-color="#312e81"/>
                </linearGradient>
              </defs>
              <rect width="100%" height="100%" fill="url(#background)"/>
              <circle cx="{width * 0.82}" cy="{height * 0.18}" r="{Math.Min(width, height) / 3}" fill="#f97316" opacity="0.22"/>
              <text x="72" y="112" fill="#fdba74" font-family="Arial,sans-serif" font-size="26" font-weight="700">ETOMS · GIẢI ĐẤU ESPORTS</text>
              <text x="72" y="{titleY}" fill="#ffffff" font-family="Arial,sans-serif" font-size="{titleSize}" font-weight="800">{title}</text>
              <text x="74" y="{titleY + 68}" fill="#cbd5e1" font-family="Arial,sans-serif" font-size="34">{game} · {landing.StartAt:dd/MM/yyyy}</text>
              <text x="74" y="{height - 110}" fill="#ffffff" font-family="Arial,sans-serif" font-size="30" font-weight="700">Quét mã để xem giải và đăng ký</text>
              <rect x="{qrX - 18}" y="{qrY - 18}" width="{qrSize + 36}" height="{qrSize + 36}" rx="16" fill="#ffffff"/>
              <image x="{qrX}" y="{qrY}" width="{qrSize}" height="{qrSize}" href="data:image/svg+xml;base64,{qr}"/>
              <text x="72" y="{height - 62}" fill="#cbd5e1" font-family="Arial,sans-serif" font-size="16">{action}</text>
            </svg>
            """;
    }

    private static string CreateTournamentLogoSvg(TournamentLandingResponse landing, int width, int height)
    {
        var title = SecurityElement.Escape(landing.Name) ?? string.Empty;
        var initials = string.Concat(landing.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Take(3)
            .Select(word => char.ToUpperInvariant(word[0])));
        var safeInitials = SecurityElement.Escape(initials) ?? string.Empty;
        return $"""
            <svg xmlns="http://www.w3.org/2000/svg" width="{width}" height="{height}" viewBox="0 0 {width} {height}">
              <defs>
                <linearGradient id="logo" x1="0" y1="0" x2="1" y2="1">
                  <stop offset="0" stop-color="#f97316"/>
                  <stop offset="1" stop-color="#4f46e5"/>
                </linearGradient>
              </defs>
              <rect x="12" y="12" width="488" height="488" rx="112" fill="#111827"/>
              <path d="M256 42 451 155v202L256 470 61 357V155Z" fill="url(#logo)"/>
              <text x="256" y="285" text-anchor="middle" fill="#fff" font-family="Arial,sans-serif" font-size="110" font-weight="800">{safeInitials}</text>
              <text x="256" y="425" text-anchor="middle" fill="#fff" font-family="Arial,sans-serif" font-size="24" font-weight="700">{title}</text>
            </svg>
            """;
    }

    private static string CreateAvatarFrameSvg(TournamentLandingResponse landing, int width, int height)
    {
        var initials = string.Concat(landing.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Take(2)
            .Select(word => char.ToUpperInvariant(word[0])));
        var safeInitials = SecurityElement.Escape(initials) ?? string.Empty;
        return $"""
            <svg xmlns="http://www.w3.org/2000/svg" width="{width}" height="{height}" viewBox="0 0 {width} {height}">
              <circle cx="540" cy="540" r="510" fill="none" stroke="#f97316" stroke-width="38"/>
              <circle cx="540" cy="540" r="474" fill="none" stroke="#4f46e5" stroke-width="18"/>
              <path d="M200 84h190l-78 78H278v34h150l-78 78H200zM880 84H690l78 78h34v34H652l78 78h150zM200 996h190l-78-78h-34v-34h150l-78-78H200zM880 996H690l78-78h34v-34H652l78-78h150z" fill="#111827"/>
              <rect x="390" y="872" width="300" height="90" rx="24" fill="#111827"/>
              <text x="540" y="932" text-anchor="middle" fill="#fff" font-family="Arial,sans-serif" font-size="38" font-weight="800">{safeInitials}</text>
            </svg>
            """;
    }
}
