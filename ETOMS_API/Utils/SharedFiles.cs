using System.Security.Claims;
using ETOMS_API.Repositories;
using MySql.Data.MySqlClient;

namespace ETOMS_API.Utils;

// 18/11/2026 — "Ảnh của máy này không lưu trên máy khác": tệp tải lên chỉ nằm trên đĩa máy tải lên (wwwroot/uploads,
// App_Data/private-uploads không có trong git), DB chung chỉ lưu đường dẫn => máy khác mở ra 404.
// Cách làm (không thêm bảng): mỗi tệp tải lên lưu thêm MỘT bản vào bảng có sẵn Attachment, cột Content (LONGBLOB):
//   OwnerType = ACCOUNT, OwnerID = UploadedByAccountID = người tải lên, FileUrl = khóa tệp
//   ("/uploads/..." cho tệp công khai, "private/..." cho tệp riêng tư).
// Máy nào thiếu tệp trên đĩa thì lấy bản trong DB về và lưu lại (lần sau đọc thẳng từ đĩa).
// Đọc/ghi bằng SQL riêng, KHÔNG ánh xạ cột Content vào EF => các truy vấn Attachment khác không kéo nội dung tệp về.
// DB chưa có cột Content (PendingSchema.AttachmentContent = false) thì mọi hàm ở đây không làm gì.
public static class SharedFiles
{
    private static string? _connectionString;
    private static string _webRoot = "";
    private static string _privateRoot = "";
    private static IHttpContextAccessor? _http;
    private static ILogger? _log;

    public static void Configure(string connectionString, string webRoot, string privateRoot, IHttpContextAccessor http, ILogger log)
    {
        _connectionString = connectionString;
        _webRoot = Path.GetFullPath(webRoot);
        _privateRoot = Path.GetFullPath(privateRoot);
        _http = http;
        _log = log;
    }

    private static bool Enabled => _connectionString != null && PendingSchema.AttachmentContent;

    // Khóa của tệp theo đường dẫn đầy đủ; null nếu tệp không nằm trong hai thư mục tải lên.
    public static string? KeyOf(string fullPath)
    {
        var full = Path.GetFullPath(fullPath);
        var uploads = Path.Combine(_webRoot, "uploads") + Path.DirectorySeparatorChar;
        if (full.StartsWith(uploads, StringComparison.OrdinalIgnoreCase))
            return "/" + Path.GetRelativePath(_webRoot, full).Replace('\\', '/');
        var priv = _privateRoot + Path.DirectorySeparatorChar;
        if (full.StartsWith(priv, StringComparison.OrdinalIgnoreCase))
            return "private/" + Path.GetRelativePath(_privateRoot, full).Replace('\\', '/');
        return null;
    }

    private static string FileTypeOf(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".jpg" or ".jpeg" or ".png" or ".gif" or ".webp" or ".svg" or ".bmp" => "IMAGE",
        ".mp4" or ".webm" or ".mov" or ".mkv" => "VIDEO",
        ".rofl" or ".replay" or ".dem" => "REPLAY",
        _ => "DOCUMENT"
    };

    private static int? CurrentAccountId()
    {
        var user = _http?.HttpContext?.User;
        var raw = user?.FindFirstValue("sub") ?? user?.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(raw, out var id) && id > 0 ? id : null;
    }

    /// <summary>Gọi NGAY SAU khi ghi xong tệp tải lên: lưu một bản vào DB chung. Lỗi chỉ ghi log, không làm hỏng việc tải lên.</summary>
    public static async Task SaveAsync(string fullPath, int? uploaderAccountId = null, CancellationToken ct = default)
    {
        if (!Enabled) return;
        var key = KeyOf(fullPath);
        if (key is null || !File.Exists(fullPath)) return;
        try
        {
            var bytes = await File.ReadAllBytesAsync(fullPath, ct);
            if (bytes.Length == 0) return;
            await using var conn = new MySqlConnection(_connectionString);
            await conn.OpenAsync(CancellationToken.None);   // không kèm token huỷ: huỷ giữa lúc bắt tay SSL làm MySql.Data ném lỗi trong callback
            var uploader = uploaderAccountId ?? CurrentAccountId();
            if (uploader is null)
            {
                // Không có người dùng trong ngữ cảnh (vd đồng bộ tệp cũ lúc khởi động): ghi cho tài khoản quản trị đầu tiên.
                await using var adm = new MySqlCommand("SELECT AccountID FROM Account WHERE Role = 'ADMIN' ORDER BY AccountID LIMIT 1", conn);
                uploader = Convert.ToInt32(await adm.ExecuteScalarAsync(ct));
            }
            await using var cmd = new MySqlCommand(
                "INSERT INTO Attachment (OwnerType, OwnerID, FileUrl, FileType, SizeBytes, UploadedByAccountID, Content) " +
                "SELECT 'ACCOUNT', @u, @k, @t, @s, @u, @c FROM DUAL " +
                "WHERE NOT EXISTS (SELECT 1 FROM Attachment WHERE FileUrl = @k AND Content IS NOT NULL)", conn);
            cmd.Parameters.AddWithValue("@u", uploader);
            cmd.Parameters.AddWithValue("@k", key);
            cmd.Parameters.AddWithValue("@t", FileTypeOf(fullPath));
            cmd.Parameters.AddWithValue("@s", (long)bytes.Length);
            cmd.Parameters.AddWithValue("@c", bytes);
            await cmd.ExecuteNonQueryAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log?.LogWarning(ex, "Không lưu được bản chung của tệp {Key} vào DB (tệp vẫn có trên máy này).", key);
        }
    }

    /// <summary>Tệp không có trên đĩa máy này: lấy bản trong DB chung về và lưu lại. Trả true nếu đã lấy được.</summary>
    public static async Task<bool> RestoreAsync(string fullPath, CancellationToken ct = default)
    {
        if (!Enabled || File.Exists(fullPath)) return File.Exists(fullPath);
        var key = KeyOf(fullPath);
        if (key is null) return false;
        try
        {
            await using var conn = new MySqlConnection(_connectionString);
            await conn.OpenAsync(CancellationToken.None);   // không kèm token huỷ: huỷ giữa lúc bắt tay SSL làm MySql.Data ném lỗi trong callback
            await using var cmd = new MySqlCommand("SELECT Content FROM Attachment WHERE FileUrl = @k AND Content IS NOT NULL ORDER BY AttachmentID DESC LIMIT 1", conn);
            cmd.Parameters.AddWithValue("@k", key);
            if (await cmd.ExecuteScalarAsync(ct) is not byte[] bytes || bytes.Length == 0) return false;
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            var temp = fullPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            await File.WriteAllBytesAsync(temp, bytes, ct);
            File.Move(temp, fullPath, overwrite: true);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log?.LogWarning(ex, "Không lấy được tệp {Key} từ DB chung.", key);
            return false;
        }
    }

    /// <summary>Đồng bộ tệp CŨ của máy này lên DB (chạy nền lúc khởi động): tệp nào DB chưa có bản thì lưu.</summary>
    public static async Task BackfillAsync(CancellationToken ct)
    {
        if (!Enabled) return;
        try
        {
            var have = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            await using (var conn = new MySqlConnection(_connectionString))
            {
                await conn.OpenAsync(CancellationToken.None);   // không kèm token huỷ: huỷ giữa lúc bắt tay SSL làm MySql.Data ném lỗi trong callback
                await using var cmd = new MySqlCommand("SELECT FileUrl FROM Attachment WHERE Content IS NOT NULL", conn);
                await using var r = await cmd.ExecuteReaderAsync(ct);
                while (await r.ReadAsync(ct)) have.Add(r.GetString(0));
            }
            var roots = new[] { Path.Combine(_webRoot, "uploads"), _privateRoot }.Where(Directory.Exists);
            foreach (var file in roots.SelectMany(root => Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)))
            {
                ct.ThrowIfCancellationRequested();
                var key = KeyOf(file);
                // uploads/demo nằm sẵn trong git ở mọi máy; tệp tạm .tmp bỏ qua.
                if (key is null || have.Contains(key) || key.StartsWith("/uploads/demo/", StringComparison.OrdinalIgnoreCase) || file.EndsWith(".tmp")) continue;
                if (new FileInfo(file).Length > 50L * 1024 * 1024) continue;
                await SaveAsync(file, null, ct);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log?.LogWarning(ex, "Đồng bộ tệp cũ lên DB chung bị dừng giữa chừng.");
        }
    }
}
