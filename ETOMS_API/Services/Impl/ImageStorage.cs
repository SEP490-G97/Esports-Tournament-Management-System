using ETOMS_API.Services.Interface;

namespace ETOMS_API.Services.Impl
{
    public class ImageStorage : IImageStorage
    {
        private const string UrlPrefix = "/uploads/";
        private readonly string _root;
        private readonly ILogger<ImageStorage> _log;

        public ImageStorage(IWebHostEnvironment env, ILogger<ImageStorage> log)
        {
            var webRoot = env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot");
            _root = Path.GetFullPath(Path.Combine(webRoot, "uploads"));
            _log = log;
        }

        public async Task<ImageSaveResult> SaveAsync(IFormFile? file, string folder, long maxBytes, CancellationToken ct = default)
        {
            if (file == null || file.Length == 0) return new ImageSaveResult(ImageError.Missing);
            if (file.Length > maxBytes) return new ImageSaveResult(ImageError.TooLarge);

            // Không tin Content-Type / tên file do client gửi: đọc chữ ký byte để xác định loại thật.
            var header = new byte[12];
            int read;
            await using (var probe = file.OpenReadStream())
            {
                read = await probe.ReadAsync(header.AsMemory(0, header.Length), ct);
            }
            var ext = DetectExtension(header, read);
            if (ext == null) return new ImageSaveResult(ImageError.BadType);

            var safeFolder = SanitizeFolder(folder);
            var dir = Path.GetFullPath(Path.Combine(_root, safeFolder));
            if (!IsUnderRoot(dir)) return new ImageSaveResult(ImageError.BadType);
            Directory.CreateDirectory(dir);

            var name = Guid.NewGuid().ToString("N") + ext;
            var full = Path.Combine(dir, name);
            await using (var input = file.OpenReadStream())
            await using (var output = new FileStream(full, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                await input.CopyToAsync(output, ct);
            }

            return new ImageSaveResult(ImageError.None, UrlPrefix + safeFolder.Replace('\\', '/') + "/" + name);
        }

        public void TryDelete(string? url)
        {
            if (string.IsNullOrWhiteSpace(url) || !url.StartsWith(UrlPrefix, StringComparison.Ordinal)) return;
            try
            {
                var relative = url.Substring(UrlPrefix.Length).Replace('/', Path.DirectorySeparatorChar);
                var full = Path.GetFullPath(Path.Combine(_root, relative));
                if (IsUnderRoot(full) && File.Exists(full)) File.Delete(full);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Không xóa được file ảnh {Url}", url);
            }
        }

        private bool IsUnderRoot(string fullPath) =>
            fullPath.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal);

        private static string SanitizeFolder(string folder)
        {
            var parts = folder.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(p => p != "." && p != "..")
                .Select(p => new string(p.Where(c => char.IsLetterOrDigit(c) || c == '-' || c == '_').ToArray()))
                .Where(p => p.Length > 0);
            var joined = string.Join("/", parts);
            return joined.Length == 0 ? "misc" : joined;
        }

        private static string? DetectExtension(byte[] h, int n)
        {
            if (n >= 8 && h[0] == 0x89 && h[1] == 0x50 && h[2] == 0x4E && h[3] == 0x47 &&
                h[4] == 0x0D && h[5] == 0x0A && h[6] == 0x1A && h[7] == 0x0A) return ".png";
            if (n >= 3 && h[0] == 0xFF && h[1] == 0xD8 && h[2] == 0xFF) return ".jpg";
            if (n >= 12 && h[0] == 'R' && h[1] == 'I' && h[2] == 'F' && h[3] == 'F' &&
                h[8] == 'W' && h[9] == 'E' && h[10] == 'B' && h[11] == 'P') return ".webp";
            return null; // SVG/GIF/… bị từ chối có chủ đích (SVG có thể chứa script)
        }
    }
}
