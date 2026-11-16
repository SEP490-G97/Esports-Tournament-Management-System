using System.Text.Encodings.Web;
using ETOMS_API.Services.Interface;
using Microsoft.AspNetCore.Mvc;

namespace ETOMS_API.Controllers;

[Route("share/tournaments")]
[ApiExplorerSettings(IgnoreApi = true)]
public sealed class TournamentShareController : ControllerBase
{
    private readonly ITournamentMarketingService _marketingService;

    public TournamentShareController(ITournamentMarketingService marketingService)
    {
        _marketingService = marketingService;
    }

    [HttpGet("{tournamentId:int}")]
    public async Task<IActionResult> GetSharePage(
        int tournamentId,
        [FromQuery(Name = "ref")] string? refCode,
        CancellationToken cancellationToken)
    {
        var landingResult = await _marketingService.GetLandingAsync(tournamentId, cancellationToken);
        if (!landingResult.IsSuccess)
        {
            return NotFound("Không tìm thấy giải đấu công khai.");
        }

        var urlResult = await _marketingService.GetRegistrationUrlAsync(
            tournamentId,
            refCode,
            new Uri($"{Request.Scheme}://{Request.Host}{Request.PathBase}/"),
            cancellationToken);
        if (!urlResult.IsSuccess)
        {
            return NotFound("Liên kết giới thiệu không hợp lệ.");
        }

        if (!string.IsNullOrWhiteSpace(refCode))
        {
            var normalizedCode = refCode.Trim().ToUpperInvariant();
            var sessionKey = $"affiliate-visit:{normalizedCode}";
            if (HttpContext.Session.GetString(sessionKey) is null)
            {
                var visitResult = await _marketingService.TrackVisitAsync(normalizedCode, cancellationToken);
                if (!visitResult.IsSuccess) return NotFound("Liên kết cộng tác viên không hợp lệ.");
                HttpContext.Session.SetString(sessionKey, "1");
            }
        }

        var landing = landingResult.Data!;
        var encoder = HtmlEncoder.Default;
        var title = encoder.Encode($"{landing.Name} · ETOMS");
        var description = encoder.Encode(landing.Description ?? $"Xem thông tin và đăng ký giải {landing.Name} trên ETOMS.");
        var shareUrl = encoder.Encode(new Uri(
            $"{Request.Scheme}://{Request.Host}{Request.PathBase}/share/tournaments/{tournamentId}" +
            (string.IsNullOrWhiteSpace(refCode) ? string.Empty : $"?ref={Uri.EscapeDataString(refCode.Trim().ToUpperInvariant())}")).ToString());
        var landingPath = $"marketing/tournament.html?tournamentId={tournamentId}";
        if (!string.IsNullOrWhiteSpace(refCode))
        {
            landingPath += $"&ref={Uri.EscapeDataString(refCode.Trim().ToUpperInvariant())}";
        }
        var landingUrl = encoder.Encode(new Uri(
            new Uri($"{Request.Scheme}://{Request.Host}{Request.PathBase}/"),
            landingPath).ToString());
        var imageUrl = ResolveImageUrl(landing.BannerUrl);
        var imageMeta = imageUrl.Length == 0 ? string.Empty : $"<meta property=\"og:image\" content=\"{imageUrl}\">";

        var html = $$"""
            <!doctype html>
            <html lang="vi">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width, initial-scale=1">
              <title>{{title}}</title>
              <meta name="description" content="{{description}}">
              <meta property="og:type" content="website">
              <meta property="og:title" content="{{title}}">
              <meta property="og:description" content="{{description}}">
              <meta property="og:url" content="{{shareUrl}}">
              {{imageMeta}}
              <meta name="twitter:card" content="{{(imageUrl.Length == 0 ? "summary" : "summary_large_image")}}">
              <style>
                body{margin:0;background:#111827;color:#fff;font:16px/1.6 Arial,sans-serif}
                main{max-width:760px;margin:8vh auto;padding:32px}
                article{padding:clamp(24px,6vw,56px);border-radius:24px;background:linear-gradient(130deg,#1f2937,#312e81)}
                img{display:block;max-width:100%;max-height:360px;object-fit:cover;border-radius:16px;margin:24px 0}
                a{display:inline-block;padding:12px 20px;border-radius:10px;background:#f97316;color:#fff;text-decoration:none;font-weight:bold}
                .muted{color:#cbd5e1}
              </style>
            </head>
            <body>
              <main><article>
                <p class="muted">{{encoder.Encode(landing.GameName)}} · {{encoder.Encode(landing.GameShortCode)}}</p>
                <h1>{{title}}</h1>
                <p>{{description}}</p>
                {{(imageUrl.Length == 0 ? string.Empty : $"<img src=\"{imageUrl}\" alt=\"Ảnh giới thiệu giải\">")}}
                <p class="muted">Bắt đầu {{landing.StartAt:dd/MM/yyyy}} · Đã đăng ký {{landing.RegisteredTeams}}/{{landing.MaxTeams}} đội</p>
                <a href="{{landingUrl}}">Xem thông tin và đăng ký</a>
              </article></main>
            </body>
            </html>
            """;

        return Content(html, "text/html; charset=utf-8");
    }

    private string ResolveImageUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var baseUri = new Uri($"{Request.Scheme}://{Request.Host}{Request.PathBase}/");
        if (!Uri.TryCreate(baseUri, value, out var image)
            || (image.Scheme != Uri.UriSchemeHttps && image.Scheme != Uri.UriSchemeHttp))
        {
            return string.Empty;
        }

        return HtmlEncoder.Default.Encode(image.ToString());
    }
}
