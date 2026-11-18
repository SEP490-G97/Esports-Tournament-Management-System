using System.Text;
using System.Text.RegularExpressions;

namespace ETOMS_API.Utils;

// Giấy tờ đơn vị lưu ở Attachment (OwnerType = ORGANIZATION) — bảng này không có cột "loại giấy tờ"
// hay "tên tệp gốc", nên hai thông tin đó nằm trong tên tệp lưu trữ:
//   d{chỉ số loại}-{tên gốc dạng base64url}-{guid}.{đuôi}
// base64url chỉ gồm chữ/số/'-'/'_' nên an toàn trên mọi hệ thống tệp; tên gốc cắt ở 60 byte UTF-8.
public static partial class OrganizationDocumentName
{
    private const int MaxNameBytes = 60; // giữ tên tệp lưu trữ ngắn (~120 ký tự), tránh vượt giới hạn đường dẫn 260 ký tự của Windows

    public static string Prefix(int typeIndex, string? originalFileName)
    {
        if (typeIndex < 0) throw new ArgumentOutOfRangeException(nameof(typeIndex));
        var name = Path.GetFileName(originalFileName ?? string.Empty);
        return $"d{typeIndex}-{ToBase64Url(Truncate(name))}-";
    }

    public static (int? TypeIndex, string? OriginalName) Parse(string? fileUrl)
    {
        var fileName = Path.GetFileName(fileUrl ?? string.Empty);
        var match = Pattern().Match(fileName);
        if (!match.Success || !int.TryParse(match.Groups[1].Value, out var index)) return (null, null);
        try
        {
            var name = Encoding.UTF8.GetString(FromBase64Url(match.Groups[2].Value));
            return (index, string.IsNullOrWhiteSpace(name) ? null : name);
        }
        catch (FormatException)
        {
            return (index, null);
        }
    }

    private static string Truncate(string name)
    {
        if (Encoding.UTF8.GetByteCount(name) <= MaxNameBytes) return name;
        var builder = new StringBuilder();
        foreach (var rune in name.EnumerateRunes())
        {
            if (Encoding.UTF8.GetByteCount(builder.ToString() + rune) > MaxNameBytes) break;
            builder.Append(rune);
        }
        return builder.ToString();
    }

    private static string ToBase64Url(string text) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(text)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] FromBase64Url(string value)
    {
        var s = value.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(s.PadRight(s.Length + (4 - s.Length % 4) % 4, '='));
    }

    // GUID 32 ký tự hex không có '-', nên phần base64url (có thể chứa '-') nằm trọn giữa "d{n}-" và "-{guid}".
    [GeneratedRegex(@"^d(\d{1,3})-([A-Za-z0-9_-]*)-[0-9a-f]{32}\.[A-Za-z0-9]{1,5}$")]
    private static partial Regex Pattern();
}
