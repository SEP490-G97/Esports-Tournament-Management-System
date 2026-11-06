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

    public PrivateFileStorage(IWebHostEnvironment environment)
    {
        _rootDirectory = Path.Combine(environment.ContentRootPath, "App_Data", "private-uploads");
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

    public FileStream OpenRead(string relativePath)
        => new(ResolvePath(relativePath), FileMode.Open, FileAccess.Read, FileShare.Read);

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
