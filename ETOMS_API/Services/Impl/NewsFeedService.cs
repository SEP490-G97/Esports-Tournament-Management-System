using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using ETOMS_API.Models;
using ETOMS_API.Repositories.Interface;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace ETOMS_API.Services.Impl;

// Trang Tin tức: tin nội bộ do Admin đăng (ContentItem Kind = NEWS) + tin esports lấy từ RSS công khai của các báo.
// Tin ngoài CHỈ lấy tiêu đề, mô tả ngắn, ảnh đại diện mà nguồn cấp trong RSS và đường dẫn về bài gốc — không chép
// nội dung bài. Mỗi nguồn nhớ 30 phút; nguồn lỗi/chậm thì bỏ qua (nhớ trạng thái lỗi 5 phút), không làm hỏng trang.
public sealed class NewsFeedService
{
    public sealed record Source(string Key, string Name, string FeedUrl, string[]? Keywords);

    // Đã kiểm 16/11/2026: các nguồn trả RSS thật. Báo tổng hợp thể thao lọc theo từ khóa game để chỉ giữ tin esports.
    public static readonly IReadOnlyList<Source> Sources = new[]
    {
        new Source("gamek", "GameK", "https://gamek.vn/esport.rss", null),
        new Source("tuoitre", "Tuổi Trẻ", "https://tuoitre.vn/rss/the-thao.rss", EsportsKeywords()),
        new Source("thanhnien", "Thanh Niên", "https://thanhnien.vn/rss/the-thao.rss", EsportsKeywords()),
    };

    private static string[] EsportsKeywords() => new[]
    {
        "esport", "e-sport", "thể thao điện tử", "game thủ", "lmht", "liên minh huyền thoại", "liên quân", "pubg",
        "valorant", "fc online", "tốc chiến", "cktg", "free fire", "dota", "counter-strike", "cs2", "mobile legends"
    };

    // Url = bài gốc; ReaderUrl = trang đọc tin NGAY TRÊN ETOMS (tin báo: tiêu đề + ảnh + tóm tắt RSS + nút đọc toàn bài).
    // Description = đoạn mô tả dài hơn do báo cung cấp qua RSS (không chép toàn văn bài — bản quyền thuộc báo gốc).
    public sealed record NewsItem(string Id, string Source, string SourceName, string Title, string? Summary,
        string Url, string? ImageUrl, DateTime? PublishedAt, bool IsInternal, bool IsPinned, string? Description = null)
    {
        public string ReaderUrl => IsInternal ? Url : "/news/index.html?ext=" + Uri.EscapeDataString(Id);
        public string? GameCode { get; } = DetectGame(Title + " " + Summary);
    }

    public sealed record SourceStatus(string Key, string Name, bool Ok, int Count);

    private static readonly HttpClient Client = CreateClient();
    private const int MaxFeedBytes = 3 * 1024 * 1024;
    private static readonly TimeSpan CacheFor = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan FailedCacheFor = TimeSpan.FromMinutes(5);

    private readonly IUnitOfWork _uow;
    private readonly IMemoryCache _cache;
    private readonly IWebHostEnvironment _env;

    public NewsFeedService(IUnitOfWork uow, IMemoryCache cache, IWebHostEnvironment env)
    {
        _uow = uow;
        _cache = cache;
        _env = env;
    }

    // Nhận ra tin nói về game nào theo từ khóa ở tiêu đề + tóm tắt (RSS báo không gắn game).
    // Tốc Chiến đứng trước LMHT vì tên đầy đủ "Liên Minh Huyền Thoại: Tốc Chiến" chứa cả hai.
    private static readonly (string Code, string[] Keywords)[] GameKeywords =
    {
        ("LMTC", new[] { "tốc chiến", "wild rift" }),
        ("LOL", new[] { "lmht", "liên minh huyền thoại", "league of legends", "cktg", "lck", "vcs", "faker", "msi 20" }),
        ("VAL", new[] { "valorant", "vct" }),
        ("LQM", new[] { "liên quân", "arena of valor", "aov", "đấu trường danh vọng", "đtdv" }),
        ("PUM", new[] { "pubg" }),
        ("FCO", new[] { "fc online", "fifa online", "fco" }),
    };

    public static string? DetectGame(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var lower = text.ToLower(new CultureInfo("vi-VN")).Normalize(NormalizationForm.FormC);
        foreach (var (code, keys) in GameKeywords)
            if (keys.Any(k => Regex.IsMatch(lower, @"(?<![\p{L}\p{N}])" + Regex.Escape(k) + @"(?![\p{L}\p{N}])"))) return code;
        return null;
    }

    public sealed record GameInfo(string Code, string Title, string? LogoUrl, string? BannerUrl);

    // Game đang hoạt động cho thanh lọc + nền trang. Ảnh nền: Game.BannerUrl, nếu trống thì tệp
    // wwwroot/images/games/banners/<mã>.jpg (thả ảnh vào là dùng, không cần sửa DB). Nhớ 10 phút.
    public async Task<List<GameInfo>> GetGamesAsync(CancellationToken ct)
    {
        if (_cache.TryGetValue("news-games", out List<GameInfo>? cached) && cached is not null) return cached;
        var games = await _uow.Repository<Game>().Query().AsNoTracking()
            .Where(g => g.Status == "ACTIVE").OrderBy(g => g.GameId)
            .Select(g => new { g.ShortCode, g.Title, g.LogoUrl, g.BannerUrl }).ToListAsync(ct);
        var list = games.Select(g => new GameInfo(g.ShortCode, g.Title, g.LogoUrl, g.BannerUrl ?? LocalBanner(g.ShortCode))).ToList();
        _cache.Set("news-games", list, TimeSpan.FromMinutes(10));
        return list;
    }

    private string? LocalBanner(string code)
    {
        foreach (var ext in new[] { ".jpg", ".webp", ".png" })
        {
            var rel = "images/games/banners/" + code.ToLowerInvariant() + ext;
            if (File.Exists(Path.Combine(_env.WebRootPath ?? "wwwroot", rel))) return "/" + rel;
        }
        return null;
    }

    private static HttpClient CreateClient()
    {
        var handler = new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All, AllowAutoRedirect = true, MaxAutomaticRedirections = 3 };
        var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(8) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (compatible; ETOMS-NewsBot/1.0)");
        return client;
    }

    // Tin nội bộ + tin ngoài, mới nhất trước (tin nội bộ được ghim luôn đứng đầu).
    public async Task<(List<NewsItem> Items, List<SourceStatus> Sources)> GetAllAsync(CancellationToken ct)
    {
        var internalNews = await _uow.Repository<ContentItem>().Query().AsNoTracking()
            .Where(c => c.Kind == "NEWS" && c.Status == "PUBLISHED")
            .OrderByDescending(c => c.PublishedAt)
            .Take(100)
            .Select(c => new { c.ContentItemId, c.Title, c.Body, c.PublishedAt, c.IsPinned })
            .ToListAsync(ct);
        var items = internalNews.Select(c => new NewsItem("etoms-" + c.ContentItemId, "etoms", "ETOMS", c.Title,
            Shorten(StripHtml(c.Body), 220), "/news/index.html?id=" + c.ContentItemId, null, c.PublishedAt, true, c.IsPinned)).ToList();

        var statuses = new List<SourceStatus> { new("etoms", "ETOMS", true, items.Count) };
        var results = await Task.WhenAll(Sources.Select(s => GetSourceAsync(s, ct)));
        for (int i = 0; i < Sources.Count; i++)
        {
            var (ok, list) = results[i];
            statuses.Add(new SourceStatus(Sources[i].Key, Sources[i].Name, ok, list.Count));
            items.AddRange(list);
        }

        var ordered = items
            .GroupBy(x => x.Url).Select(g => g.First())
            .OrderByDescending(x => x.IsInternal && x.IsPinned)
            .ThenByDescending(x => x.PublishedAt ?? DateTime.MinValue)
            .ToList();
        return (ordered, statuses);
    }

    public async Task<ContentItem?> GetInternalAsync(int id, CancellationToken ct)
        => await _uow.Repository<ContentItem>().Query().AsNoTracking()
            .Include(c => c.AuthorAccount)
            .FirstOrDefaultAsync(c => c.ContentItemId == id && c.Kind == "NEWS" && c.Status == "PUBLISHED", ct);

    private async Task<(bool Ok, List<NewsItem> Items)> GetSourceAsync(Source source, CancellationToken ct)
    {
        var key = "news-feed:" + source.Key;
        if (_cache.TryGetValue(key, out (bool, List<NewsItem>) cached)) return cached;
        (bool, List<NewsItem>) result;
        try
        {
            result = (true, await FetchAsync(source, ct));
            _cache.Set(key, result, CacheFor);
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            // Nguồn lỗi/chậm: không chặn cả trang, thử lại sau 5 phút.
            result = (false, new List<NewsItem>());
            _cache.Set(key, result, FailedCacheFor);
        }
        return result;
    }

    private static async Task<List<NewsItem>> FetchAsync(Source source, CancellationToken ct)
    {
        using var response = await Client.GetAsync(source.FeedUrl, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength is > MaxFeedBytes) throw new InvalidDataException("RSS quá lớn.");
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await stream.ReadAsync(chunk, ct)) > 0)
        {
            buffer.Write(chunk, 0, read);
            if (buffer.Length > MaxFeedBytes) throw new InvalidDataException("RSS quá lớn.");
        }
        buffer.Position = 0;

        // Không xử lý DTD/thực thể ngoài (chống XXE), giới hạn kích thước thực thể.
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, XmlResolver = null, MaxCharactersFromEntities = 1024, Async = false };
        using var reader = XmlReader.Create(buffer, settings);
        var doc = XDocument.Load(reader);
        XNamespace media = "http://search.yahoo.com/mrss/";

        var list = new List<NewsItem>();
        foreach (var item in doc.Descendants("item").Take(60))
        {
            var title = Clean(item.Element("title")?.Value);
            var link = item.Element("link")?.Value?.Trim();
            if (string.IsNullOrEmpty(title) || !IsHttpUrl(link)) continue;
            var descriptionHtml = item.Element("description")?.Value ?? string.Empty;
            var summary = Shorten(Clean(StripHtml(descriptionHtml)), 220);
            if (source.Keywords is not null && !MatchesAny(title + " " + summary, source.Keywords)) continue;

            var image = item.Element("enclosure")?.Attribute("url")?.Value
                        ?? item.Element(media + "content")?.Attribute("url")?.Value
                        ?? item.Element(media + "thumbnail")?.Attribute("url")?.Value
                        ?? FirstImage(descriptionHtml);
            DateTime? published = DateTimeOffset.TryParse(item.Element("pubDate")?.Value, CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces, out var dto) ? dto.UtcDateTime : null;

            // Mã tin cố định theo đường dẫn bài (không theo thứ tự) để link đọc tin không đổi khi báo có tin mới.
            list.Add(new NewsItem(source.Key + "-" + StableId(link!), source.Key, source.Name, title, summary,
                link!, IsHttpUrl(image) ? image : null, published, false, false, Shorten(Clean(StripHtml(descriptionHtml)), 1200)));
        }
        return list;
    }

    private static string StableId(string link)
        => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(link)))[..16].ToLowerInvariant();

    // Một tin báo theo mã (lấy từ danh sách đã nhớ 30 phút). Null nếu tin đã trôi khỏi RSS.
    public async Task<NewsItem?> GetExternalAsync(string id, CancellationToken ct)
    {
        var (items, _) = await GetAllAsync(ct);
        return items.FirstOrDefault(i => !i.IsInternal && i.Id == id);
    }

    private static bool IsHttpUrl(string? url)
        => Uri.TryCreate(url, UriKind.Absolute, out var u) && (u.Scheme == Uri.UriSchemeHttps || u.Scheme == Uri.UriSchemeHttp);

    private static readonly Regex Tags = new("<[^>]+>", RegexOptions.Compiled);
    private static readonly Regex Img = new("<img[^>]+src\\s*=\\s*[\"']([^\"']+)[\"']", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex Spaces = new("\\s+", RegexOptions.Compiled);

    private static string StripHtml(string html) => Tags.Replace(html ?? string.Empty, " ");
    private static string? FirstImage(string html) { var m = Img.Match(html ?? string.Empty); return m.Success ? WebUtility.HtmlDecode(m.Groups[1].Value) : null; }
    private static string Clean(string? text) => Spaces.Replace(WebUtility.HtmlDecode(StripHtml(text ?? string.Empty)), " ").Trim();

    private static string? Shorten(string? text, int max)
    {
        var t = Spaces.Replace(text ?? string.Empty, " ").Trim();
        if (t.Length == 0) return null;
        return t.Length <= max ? t : t[..max].TrimEnd() + "…";
    }

    private static bool MatchesAny(string text, string[] keywords)
    {
        var lower = text.ToLower(new CultureInfo("vi-VN")).Normalize(NormalizationForm.FormC);
        return keywords.Any(k => lower.Contains(k));
    }
}
