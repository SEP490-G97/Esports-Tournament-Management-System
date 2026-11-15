namespace ETOMS_API.Utils;

/// <summary>Quy tắc cho URL do admin nhập (liên kết thông báo, ảnh/liên kết banner): chống javascript:, //host, v.v.</summary>
public static class LinkRules
{
    public const int MaxLength = 500;

    /// <summary>Rỗng/null hợp lệ. Chấp nhận "/đường-dẫn-nội-bộ" hoặc URL https tuyệt đối.</summary>
    public static bool IsValidOptional(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return true;
        url = url.Trim();
        if (url.Length > MaxLength) return false;
        if (url.Any(c => char.IsControl(c) || char.IsWhiteSpace(c) || c == '\\')) return false;

        if (url.StartsWith('/'))
            return !url.StartsWith("//", StringComparison.Ordinal);

        return Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps;
    }

    public static string? Normalize(string? url) =>
        string.IsNullOrWhiteSpace(url) ? null : url.Trim();
}