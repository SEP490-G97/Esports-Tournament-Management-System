namespace ETOMS_API.Utils;

public sealed record StoredPrivateFile(string RelativePath, string OriginalFileName);

public sealed class PrivateFileStorage
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".pdf"
    };

    private const long MaximumFileSize = 10 * 1024 * 1024;
    private readonly string _rootDirectory;
    private readonly string _demoUploadsDirectory;

    public PrivateFileStorage(IWebHostEnvironment environment)
    {
        _rootDirectory = Path.Combine(environment.ContentRootPath, "App_Data", "private-uploads");
        _demoUploadsDirectory = Path.Combine(environment.WebRootPath ?? Path.Combine(environment.ContentRootPath, "wwwroot"), "uploads");
    }

    // Mở tệp để tải về, hoặc null nếu không có trên máy chủ này. Hai nguồn:
    //  - "/uploads/..." : tệp mẫu của dữ liệu demo (wwwroot/uploads, có trong git);
    //  - còn lại        : tệp người dùng tải lên (App_Data/private-uploads, KHÔNG có trong git — máy
    //                     khác máy đã tải lên sẽ không có tệp, phải báo rõ thay vì lỗi 500).
    public FileStream? TryOpenRead(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath)) return null;
        var path = relativePath.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase)
            ? Inside(_demoUploadsDirectory, relativePath["/uploads/".Length..])
            : Inside(_rootDirectory, relativePath);
        return path is not null && File.Exists(path)
            ? new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read)
            : null;
    }

    public static string ContentTypeOf(string fileName) => Path.GetExtension(fileName).ToLowerInvariant() switch
    {
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        ".pdf" => "application/pdf",
        _ => "application/octet-stream"
    };

    private static string? Inside(string root, string relativePath)
    {
        var fullPath = Path.GetFullPath(Path.Combine(root, relativePath.TrimStart('/', '\\')));
        var rootWithSeparator = Path.GetFullPath(root) + Path.DirectorySeparatorChar;
        return fullPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase) ? fullPath : null;
    }

    public async Task<StoredPrivateFile> SaveAsync(
        IFormFile file,
        string category,
        CancellationToken cancellationToken = default)
    {
        if (file.Length <= 0)
        {
            throw new InvalidDataException("Tệp tải lên đang trống.");
        }

        if (file.Length > MaximumFileSize)
        {
            throw new InvalidDataException("Tệp tải lên vượt quá giới hạn 10 MB.");
        }

        var extension = Path.GetExtension(file.FileName);
        if (!AllowedExtensions.Contains(extension))
        {
            throw new InvalidDataException("Chỉ chấp nhận tệp JPG, PNG hoặc PDF.");
        }

        var safeCategory = string.Concat(category.Where(char.IsLetterOrDigit)).ToLowerInvariant();
        var relativePath = Path.Combine(safeCategory, $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}");
        var destination = Path.Combine(_rootDirectory, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);

        await using var stream = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await file.CopyToAsync(stream, cancellationToken);

        return new StoredPrivateFile(
            relativePath.Replace(Path.DirectorySeparatorChar, '/'),
            Path.GetFileName(file.FileName));
    }

    public Task DeleteAsync(string relativePath)
    {
        var fullPath = ResolvePath(relativePath);

        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }

        return Task.CompletedTask;
    }

    private string ResolvePath(string relativePath)
    {
        var fullPath = Path.GetFullPath(Path.Combine(_rootDirectory, relativePath));
        var rootWithSeparator = Path.GetFullPath(_rootDirectory) + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Đường dẫn tệp riêng tư không hợp lệ.");
        }

        return fullPath;
    }
}
