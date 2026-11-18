namespace ETOMS_API.Utils;

// Ảnh bìa giải (Tournament.BannerUrl): nhận tệp tải lên HOẶC liên kết bất kỳ (kể cả link trang Google Ảnh —
// RemoteImageFetcher tự lấy ảnh gốc, chống SSRF). Ảnh luôn được lưu về wwwroot/uploads/banners để không phụ thuộc
// liên kết ngoài (hết hạn, chặn hotlink). Nhận diện ảnh theo chữ ký tệp, không tin phần mở rộng/Content-Type.
public static class BannerImage
{
    public const long MaxBytes = 5 * 1024 * 1024;

    private static string? Extension(string contentType) => contentType switch
    {
        "image/jpeg" => ".jpg",
        "image/png" => ".png",
        "image/webp" => ".webp",
        "image/gif" => ".gif",
        _ => null
    };

    public static async Task<(string? Url, string? Error)> SaveAsync(IFormFile? file, string? link, IWebHostEnvironment env, CancellationToken ct)
    {
        byte[] bytes;
        if (file is { Length: > 0 })
        {
            if (file.Length > MaxBytes) return (null, "Ảnh bìa tối đa 5 MB.");
            using var ms = new MemoryStream();
            await file.CopyToAsync(ms, ct);
            bytes = ms.ToArray();
        }
        else if (!string.IsNullOrWhiteSpace(link))
        {
            var (image, error) = await RemoteImageFetcher.FetchAsync(link, ct);
            if (image is null) return (null, error);
            bytes = image.Bytes;
        }
        else return (null, "Hãy chọn tệp ảnh hoặc dán liên kết ảnh.");

        var type = RemoteImageFetcher.Sniff(bytes);
        var ext = type is null ? null : Extension(type);
        if (ext is null) return (null, "Ảnh bìa phải là JPG, PNG, WEBP hoặc GIF.");

        var folder = Path.Combine(env.WebRootPath, "uploads", "banners");
        Directory.CreateDirectory(folder);
        var name = Guid.NewGuid().ToString("N") + ext;
        await File.WriteAllBytesAsync(Path.Combine(folder, name), bytes, ct);
        await SharedFiles.SaveAsync(Path.Combine(folder, name), null, ct);   // bản chung trong DB cho máy khác
        return ("/uploads/banners/" + name, null);
    }
}
