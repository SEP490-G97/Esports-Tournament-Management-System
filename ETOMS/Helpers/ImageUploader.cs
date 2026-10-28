namespace ETOMS.Helpers;

// Tiện ích upload ảnh dùng chung (logo đội, avatar, logo game...). Kiểm định dạng + dung lượng,
// lưu vào wwwroot/uploads/<subFolder> với tên ngẫu nhiên, trả về đường dẫn tương đối.
public static class ImageUploader
{
    private static readonly string[] AllowedExt = { ".jpg", ".jpeg", ".png", ".webp" };

    public static (bool Ok, string? Url, string? Error) Save(
        IFormFile? file, IWebHostEnvironment env, string subFolder, long maxBytes = 2 * 1024 * 1024)
    {
        // Không bắt buộc: không có file thì bỏ qua, không lỗi.
        if (file is null || file.Length == 0)
            return (true, null, null);

        if (file.Length > maxBytes)
            return (false, null, $"Ảnh không được vượt quá {maxBytes / (1024 * 1024)}MB.");

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (Array.IndexOf(AllowedExt, ext) < 0 || !file.ContentType.StartsWith("image/"))
            return (false, null, "Ảnh phải có định dạng JPG, PNG hoặc WEBP.");

        var folder = Path.Combine(env.WebRootPath, "uploads", subFolder);
        Directory.CreateDirectory(folder);

        var fileName = $"{Guid.NewGuid():N}{ext}";
        var fullPath = Path.Combine(folder, fileName);
        using (var stream = new FileStream(fullPath, FileMode.Create))
        {
            file.CopyTo(stream);
        }

        return (true, $"/uploads/{subFolder}/{fileName}", null);
    }
}
