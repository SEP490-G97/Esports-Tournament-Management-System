using System.Text.RegularExpressions;

namespace ETOMS_API.Utils;

// UC-46 — nhận diện nền tảng từ link livestream và đổi sang link trình phát nhúng.
// ETOMS không lưu và không chuyển tiếp video (Report 4 §External: "The browser embeds the link
// stored in Livestream"), cũng không gọi API của nền tảng (LI-01): chỉ đổi dạng URL.
// Chỉ nhúng ba nền tảng đã biết; link khác (OTHER) chỉ hiện nút mở ở tab mới, không nhúng trang lạ.
public static class StreamEmbed
{
    public const int MaxUrlLength = 500;

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
                return null;
        }
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
