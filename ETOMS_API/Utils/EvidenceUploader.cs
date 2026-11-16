namespace ETOMS_API.Utils;

// Upload minh chứng báo cáo trận (RES-01) — ảnh/video/replay. Cùng kiểu với ImageUploader
// nhưng cho nhiều định dạng hơn và giới hạn dung lượng riêng theo loại file (BR-RES-06:
// lược đồ v2.1 chưa có bảng cấu hình minh chứng riêng theo từng game nên áp dụng chung 1 quy tắc).
public static class EvidenceUploader
{
    private static readonly (string Ext, string Kind, long MaxBytes)[] Rules =
    {
        (".jpg",  "IMAGE",  8  * 1024 * 1024),
        (".jpeg", "IMAGE",  8  * 1024 * 1024),
        (".png",  "IMAGE",  8  * 1024 * 1024),
        (".webp", "IMAGE",  8  * 1024 * 1024),
        (".mp4",  "VIDEO",  100 * 1024 * 1024),
        (".webm", "VIDEO",  100 * 1024 * 1024),
        (".mov",  "VIDEO",  100 * 1024 * 1024),
        (".rofl", "REPLAY", 30 * 1024 * 1024), // League of Legends
        (".dem",  "REPLAY", 30 * 1024 * 1024), // CS/Valorant-style
    };

    public static (bool Ok, string? Url, string? FileType, long? SizeBytes, string? Error) Save(
        IFormFile? file, IWebHostEnvironment env, string subFolder)
    {
        if (file is null || file.Length == 0)
            return (false, null, null, null, "Vui lòng đính kèm ảnh hoặc file minh chứng.");

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var rule = Array.Find(Rules, r => r.Ext == ext);
        if (rule.Ext is null)
            return (false, null, null, null, "Định dạng minh chứng không hỗ trợ. Chấp nhận ảnh (JPG/PNG/WEBP), video (MP4/WEBM/MOV) hoặc file replay (ROFL/DEM).");

        if (file.Length > rule.MaxBytes)
            return (false, null, null, null, $"File {rule.Kind.ToLowerInvariant()} không được vượt quá {rule.MaxBytes / (1024 * 1024)}MB.");

        var folder = Path.Combine(env.WebRootPath, "uploads", subFolder);
        Directory.CreateDirectory(folder);

        var fileName = $"{Guid.NewGuid():N}{ext}";
        var fullPath = Path.Combine(folder, fileName);
        using (var stream = new FileStream(fullPath, FileMode.Create))
        {
            file.CopyTo(stream);
        }

        return (true, $"/uploads/{subFolder}/{fileName}", rule.Kind, file.Length, null);
    }
}
