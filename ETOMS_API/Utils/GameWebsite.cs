namespace ETOMS_API.Utils;

// Trang chủ chính thức của từng tựa game (nút "Các tựa game" ở trang chủ). Bảng Game không có cột
// website nên giữ danh sách cố định theo ShortCode; game không có trong danh sách thì không có liên kết.
// Chỉ nhận URL https tuyệt đối — không bao giờ trả URL lấy từ dữ liệu người dùng nhập.
public static class GameWebsite
{
    private static readonly Dictionary<string, string> Urls = new(StringComparer.OrdinalIgnoreCase)
    {
        ["FCO"] = "https://fconline.garena.vn/",
        ["LOL"] = "https://www.leagueoflegends.com/vi-vn/",
        ["LMTC"] = "https://wildrift.leagueoflegends.com/vi-vn/",
        ["LQM"] = "https://lienquan.garena.vn/",
        ["PUM"] = "https://www.pubgmobile.com/",
        ["VAL"] = "https://playvalorant.com/vi-vn/"
    };

    public static string? For(string? shortCode)
    {
        if (string.IsNullOrWhiteSpace(shortCode)) return null;
        if (!Urls.TryGetValue(shortCode.Trim(), out var url)) return null;
        return IsSafe(url) ? url : null;
    }

    public static bool IsSafe(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps && !string.IsNullOrEmpty(uri.Host);
}
