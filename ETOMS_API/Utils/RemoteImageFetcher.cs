using System.Net;
using System.Net.Sockets;

namespace ETOMS_API.Utils;

// PRF-01: tải ảnh từ liên kết người dùng dán (vd "Sao chép địa chỉ hình ảnh" trên Google) để trang hồ sơ cắt
// ảnh rồi tải lên như ảnh từ máy. Không lưu gì — chỉ trả bytes ảnh cho chính người dùng.
// Chống SSRF: chỉ http/https cổng 80/443; MỌI kết nối (kể cả sau chuyển hướng) đi qua ConnectCallback, chỉ nối
// tới địa chỉ IP công khai (chặn loopback, mạng nội bộ, link-local, metadata đám mây...). Tối đa 5 MB, 10 giây,
// 3 lần chuyển hướng, và nội dung phải đúng chữ ký tệp ảnh (không tin Content-Type).
public static class RemoteImageFetcher
{
    public const long MaxBytes = 5 * 1024 * 1024;

    public sealed record Fetched(byte[] Bytes, string ContentType);

    private static readonly HttpClient Client = CreateClient();

    private static HttpClient CreateClient()
    {
        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 3,
            ConnectTimeout = TimeSpan.FromSeconds(5),
            AutomaticDecompression = DecompressionMethods.All,
            UseCookies = false,
            ConnectCallback = async (context, cancellationToken) =>
            {
                var port = context.DnsEndPoint.Port;
                if (port is not (80 or 443)) throw new HttpRequestException("Cổng không được phép.");
                var addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, cancellationToken);
                if (addresses.Length == 0 || addresses.Any(a => !IsPublic(a)))
                    throw new HttpRequestException("Địa chỉ không được phép.");
                var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
                try
                {
                    await socket.ConnectAsync(addresses, port, cancellationToken);
                    return new NetworkStream(socket, ownsSocket: true);
                }
                catch
                {
                    socket.Dispose();
                    throw;
                }
            }
        };
        var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(10) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (compatible; ETOMS-avatar/1.0)");
        client.DefaultRequestHeaders.Accept.ParseAdd("image/*");
        return client;
    }

    // Link trang kết quả Google Hình ảnh (…/imgres?imgurl=…) -> link ảnh gốc bên trong.
    public static string? Normalize(string? raw)
    {
        var text = (raw ?? string.Empty).Trim();
        if (text.Length == 0 || text.Length > 2048) return null;
        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            return null;
        if (uri.Host.Contains("google.", StringComparison.OrdinalIgnoreCase) && uri.AbsolutePath.StartsWith("/imgres", StringComparison.OrdinalIgnoreCase))
        {
            var inner = System.Web.HttpUtility.ParseQueryString(uri.Query)["imgurl"];
            return inner is null ? null : Normalize(inner);
        }
        return uri.AbsoluteUri;
    }

    public static async Task<(Fetched? Image, string? Error)> FetchAsync(string? raw, CancellationToken cancellationToken)
    {
        var url = Normalize(raw);
        if (url is null) return (null, "Liên kết không hợp lệ — hãy dán liên kết bắt đầu bằng http:// hoặc https://.");
        try
        {
            using var response = await Client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return (null, $"Máy chủ chứa ảnh trả lỗi {(int)response.StatusCode} — liên kết có thể đã hết hạn hoặc cần đăng nhập.");
            if (response.Content.Headers.ContentLength > MaxBytes)
                return (null, "Ảnh từ liên kết lớn hơn 5 MB.");

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var buffer = new MemoryStream();
            var chunk = new byte[81920];
            int read;
            while ((read = await stream.ReadAsync(chunk, cancellationToken)) > 0)
            {
                buffer.Write(chunk, 0, read);
                if (buffer.Length > MaxBytes) return (null, "Ảnh từ liên kết lớn hơn 5 MB.");
            }
            var bytes = buffer.ToArray();
            var type = Sniff(bytes);
            return type is null
                ? (null, "Liên kết không trỏ tới một tệp ảnh (có thể là trang web). Trên Google, bấm vào ảnh để mở to rồi chuột phải → \"Sao chép địa chỉ hình ảnh\".")
                : (new Fetched(bytes, type), null);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return (null, "Tải ảnh quá lâu (quá 10 giây) — hãy thử liên kết khác hoặc tải ảnh lên từ máy.");
        }
        catch (HttpRequestException)
        {
            return (null, "Không tải được ảnh từ liên kết này — hãy thử liên kết khác hoặc tải ảnh lên từ máy.");
        }
    }

    // Nhận diện ảnh theo chữ ký tệp, không tin Content-Type của máy chủ ngoài.
    public static string? Sniff(ReadOnlySpan<byte> b)
    {
        if (b.Length >= 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF) return "image/jpeg";
        if (b.Length >= 8 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47) return "image/png";
        if (b.Length >= 6 && b[0] == 'G' && b[1] == 'I' && b[2] == 'F' && b[3] == '8') return "image/gif";
        if (b.Length >= 12 && b[0] == 'R' && b[1] == 'I' && b[2] == 'F' && b[3] == 'F' && b[8] == 'W' && b[9] == 'E' && b[10] == 'B' && b[11] == 'P') return "image/webp";
        if (b.Length >= 2 && b[0] == 'B' && b[1] == 'M') return "image/bmp";
        if (b.Length >= 12 && b[4] == 'f' && b[5] == 't' && b[6] == 'y' && b[7] == 'p' && b[8] == 'a' && b[9] == 'v' && b[10] == 'i') return "image/avif";
        return null;
    }

    public static bool IsPublic(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        if (IPAddress.IsLoopback(address)) return false;
        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var b = address.GetAddressBytes();
            return !(b[0] == 0 || b[0] == 10 || b[0] == 127
                || (b[0] == 100 && b[1] >= 64 && b[1] <= 127)          // CGNAT
                || (b[0] == 169 && b[1] == 254)                         // link-local, metadata đám mây
                || (b[0] == 172 && b[1] >= 16 && b[1] <= 31)
                || (b[0] == 192 && b[1] == 168)
                || (b[0] == 192 && b[1] == 0 && b[2] == 0)
                || (b[0] == 198 && (b[1] == 18 || b[1] == 19))
                || b[0] >= 224);                                        // multicast, dành riêng, broadcast
        }
        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            if (address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6Multicast || address.Equals(IPAddress.IPv6None) || address.Equals(IPAddress.IPv6Any))
                return false;
            var b = address.GetAddressBytes();
            return (b[0] & 0xFE) != 0xFC;                                // fc00::/7 unique local
        }
        return false;
    }
}
