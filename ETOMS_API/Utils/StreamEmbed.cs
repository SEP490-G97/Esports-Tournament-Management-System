using System.Text.RegularExpressions;

namespace ETOMS_API.Utils;

// UC-46 — nhận diện nền tảng từ link livestream và đổi sang link trình phát nhúng.
// ETOMS không lưu và không chuyển tiếp video (Report 4 §External: "The browser embeds the link
// stored in Livestream"), cũng không gọi API của nền tảng (LI-01): chỉ đổi dạng URL.
// Góp ý 25/11: gắn link live của nền tảng nào cũng được. Cột Platform (CK_Livestream_Platform) chỉ có
// YOUTUBE/FACEBOOK/TWITCH/OTHER nên nền tảng mới vẫn lưu OTHER; tên nền tảng và cách nhúng suy ra từ link:
//   - PLAYER: đổi sang link trình phát chính thức của nền tảng (YouTube, Facebook, Twitch, video TikTok, Kick, Vimeo, Dailymotion);
//   - PAGE: trang lạ → nhúng thẳng trang đó (khung sandbox ở trình duyệt), kèm nút mở link nếu trang chặn nhúng;
//   - null: nền tảng đã biết là chặn nhúng (TikTok live, trang kênh YouTube, trang Facebook...) → chỉ hiện nút mở.
public static class StreamEmbed
{
    public const int MaxUrlLength = 500;
    public const string KindPlayer = "PLAYER";
    public const string KindPage = "PAGE";

    // Tên hiển thị của các nền tảng hay dùng (lưu OTHER trong DB).
    private static readonly (string Domain, string Name)[] KnownHosts =
    {
        ("tiktok.com", "TikTok"), ("kick.com", "Kick"), ("vimeo.com", "Vimeo"), ("dailymotion.com", "Dailymotion"), ("dai.ly", "Dailymotion"),
        ("bilibili.com", "Bilibili"), ("nimo.tv", "Nimo TV"), ("trovo.live", "Trovo"), ("huya.com", "Huya"), ("douyin.com", "Douyin"),
        ("instagram.com", "Instagram"), ("x.com", "X"), ("twitter.com", "X"), ("threads.net", "Threads"), ("rumble.com", "Rumble"),
        ("sooplive.co.kr", "SOOP"), ("afreecatv.com", "AfreecaTV"), ("booyah.live", "Booyah"), ("zalo.me", "Zalo")
    };

    // Nền tảng không cho trang khác nhúng (X-Frame-Options / frame-ancestors) — nhúng thẳng chỉ ra khung trống.
    private static readonly string[] NoFrameHosts =
        { "tiktok.com", "instagram.com", "x.com", "twitter.com", "threads.net", "douyin.com", "youtube.com", "youtu.be", "facebook.com", "fb.watch", "twitch.tv", "zalo.me" };

    private static readonly Regex KickChannel = new("^[A-Za-z0-9_-]{2,40}$", RegexOptions.Compiled);
    private static readonly Regex DailymotionId = new("^[A-Za-z0-9]{5,20}$", RegexOptions.Compiled);
    private static readonly HashSet<string> KickReserved = new(StringComparer.OrdinalIgnoreCase)
        { "categories", "category", "following", "search", "browse", "video", "videos", "clips", "terms-of-service", "privacy-policy" };

    private static readonly Regex YoutubeId = new("^[A-Za-z0-9_-]{11}$", RegexOptions.Compiled);
    private static readonly Regex TwitchChannel = new("^[A-Za-z0-9_]{3,25}$", RegexOptions.Compiled);
    private static readonly Regex Digits = new("^[0-9]{1,20}$", RegexOptions.Compiled);
    private static readonly HashSet<string> TwitchReserved = new(StringComparer.OrdinalIgnoreCase)
        { "directory", "videos", "search", "settings", "downloads", "p", "jobs", "turbo", "store", "subscriptions", "inventory", "wallet" };

    public static string PlatformLabel(string platform) => platform switch
    {
        "YOUTUBE" => "YouTube",
        "FACEBOOK" => "Facebook",
        "TWITCH" => "Twitch",
        _ => "Nền tảng khác"
    };

    // Link của nền tảng ngoài ba nền tảng chính: lấy tên từ tên miền (TikTok, Kick...), lạ thì hiện tên miền.
    public static string PlatformLabel(string platform, string? streamUrl)
    {
        if (platform != "OTHER" || !Uri.TryCreate(streamUrl, UriKind.Absolute, out var uri)) return PlatformLabel(platform);
        var host = uri.Host.ToLowerInvariant();
        foreach (var (domain, name) in KnownHosts) if (IsHost(host, domain)) return name;
        return host.StartsWith("www.", StringComparison.Ordinal) ? host[4..] : host;
    }

    // PLAYER = trình phát chính thức, PAGE = nhúng thẳng trang, null = không nhúng được (chỉ mở link).
    public static string? EmbedKind(string platform, string streamUrl)
    {
        var embed = EmbedUrl(platform, streamUrl);
        if (embed is null) return null;
        return string.Equals(embed, streamUrl, StringComparison.Ordinal) ? KindPage : KindPlayer;
    }

    public static string StatusLabel(string status) => status switch
    {
        "LIVE" => "Đang phát",
        "ENDED" => "Đã kết thúc",
        _ => "Sắp phát"
    };

    // Trả null khi link hợp lệ; ngược lại là thông báo lỗi tiếng Việt.
    public static string? Validate(string? raw, out Uri? uri)
    {
        uri = null;
        var url = (raw ?? string.Empty).Trim();
        if (url.Length == 0) return "Vui lòng nhập link livestream.";
        if (url.Length > MaxUrlLength) return $"Link livestream tối đa {MaxUrlLength} ký tự.";
        if (!Uri.TryCreate(url, UriKind.Absolute, out var parsed) || string.IsNullOrEmpty(parsed.Host))
            return "Link livestream không hợp lệ.";
        if (parsed.Scheme != Uri.UriSchemeHttps) return "Link livestream phải bắt đầu bằng https://";
        if (!string.IsNullOrEmpty(parsed.UserInfo)) return "Link livestream không hợp lệ.";
        uri = parsed;
        return null;
    }

    public static string DetectPlatform(Uri uri)
    {
        var host = uri.Host.ToLowerInvariant();
        if (host is "youtu.be" || IsHost(host, "youtube.com")) return "YOUTUBE";
        if (host is "fb.watch" || IsHost(host, "facebook.com")) return "FACEBOOK";
        if (IsHost(host, "twitch.tv")) return "TWITCH";
        return "OTHER";
    }

    // Twitch cần tham số parent = tên miền trang nhúng; trình duyệt tự thêm (&parent=location.hostname).
    public static string? EmbedUrl(string platform, string streamUrl)
    {
        if (!Uri.TryCreate(streamUrl, UriKind.Absolute, out var uri)) return null;
        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        switch (platform)
        {
            case "YOUTUBE":
            {
                string? id = null;
                if (uri.Host.Equals("youtu.be", StringComparison.OrdinalIgnoreCase)) id = segments.FirstOrDefault();
                else if (segments.Length >= 2 && segments[0] is "live" or "embed" or "shorts") id = segments[1];
                else if (segments.Length == 1 && segments[0] == "watch") id = QueryValue(uri, "v");
                // Link kênh dạng youtube.com/@kenh/live không có mã video → chỉ mở ở tab mới.
                return id is not null && YoutubeId.IsMatch(id) ? "https://www.youtube.com/embed/" + id : null;
            }
            case "TWITCH":
            {
                if (segments.Length >= 2 && segments[0].Equals("videos", StringComparison.OrdinalIgnoreCase) && Digits.IsMatch(segments[1]))
                    return "https://player.twitch.tv/?video=" + segments[1];
                if (segments.Length == 1 && TwitchChannel.IsMatch(segments[0]) && !TwitchReserved.Contains(segments[0]))
                    return "https://player.twitch.tv/?channel=" + segments[0].ToLowerInvariant();
                return null;
            }
            case "FACEBOOK":
            {
                bool isVideo = uri.Host.Equals("fb.watch", StringComparison.OrdinalIgnoreCase)
                    || segments.Any(s => s.Equals("videos", StringComparison.OrdinalIgnoreCase))
                    || (segments.Length >= 1 && segments[0].Equals("watch", StringComparison.OrdinalIgnoreCase));
                return isVideo
                    ? "https://www.facebook.com/plugins/video.php?show_text=false&href=" + Uri.EscapeDataString(streamUrl)
                    : null;
            }
            default:
                return OtherEmbedUrl(uri, segments, streamUrl);
        }
    }

    // Nền tảng khác: dùng trình phát chính thức nếu biết định dạng; nền tảng chặn nhúng → null; trang lạ → nhúng thẳng.
    private static string? OtherEmbedUrl(Uri uri, string[] segments, string streamUrl)
    {
        var host = uri.Host.ToLowerInvariant();
        // Video TikTok: tiktok.com/@kenh/video/{id} → trình phát nhúng chính thức. Live TikTok không cho nhúng.
        if (IsHost(host, "tiktok.com"))
            return segments.Length >= 3 && segments[1].Equals("video", StringComparison.OrdinalIgnoreCase) && Digits.IsMatch(segments[2])
                ? "https://www.tiktok.com/embed/v2/" + segments[2]
                : null;
        // Kick: kick.com/{kenh} → player.kick.com/{kenh}.
        if (IsHost(host, "kick.com"))
            return segments.Length == 1 && KickChannel.IsMatch(segments[0]) && !KickReserved.Contains(segments[0])
                ? "https://player.kick.com/" + segments[0].ToLowerInvariant()
                : null;
        // Vimeo: vimeo.com/{id} hoặc vimeo.com/event/{id}.
        if (IsHost(host, "vimeo.com"))
        {
            if (segments.Length >= 1 && Digits.IsMatch(segments[0])) return "https://player.vimeo.com/video/" + segments[0];
            if (segments.Length >= 2 && segments[0].Equals("event", StringComparison.OrdinalIgnoreCase) && Digits.IsMatch(segments[1]))
                return "https://vimeo.com/event/" + segments[1] + "/embed";
            return null;
        }
        // Dailymotion: dailymotion.com/video/{id} hoặc dai.ly/{id}.
        if (IsHost(host, "dailymotion.com") || host == "dai.ly")
        {
            var id = host == "dai.ly" ? segments.FirstOrDefault()
                : segments.Length >= 2 && segments[0].Equals("video", StringComparison.OrdinalIgnoreCase) ? segments[1] : null;
            return id is not null && DailymotionId.IsMatch(id) ? "https://www.dailymotion.com/embed/video/" + id : null;
        }
        if (NoFrameHosts.Any(d => IsHost(host, d))) return null;
        return streamUrl;
    }

    private static bool IsHost(string host, string domain) => host == domain || host.EndsWith("." + domain, StringComparison.Ordinal);

    private static string? QueryValue(Uri uri, string key)
    {
        foreach (var part in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = part.Split('=', 2);
            if (kv.Length == 2 && kv[0] == key) return Uri.UnescapeDataString(kv[1]);
        }
        return null;
    }
}
