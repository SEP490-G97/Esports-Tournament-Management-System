using System.Text.RegularExpressions;

namespace ETOMS_API.Utils;

// Số điện thoại Việt Nam (góp ý nhóm 18/11 mục 25): chỉ chữ số, đúng 10 số, bắt đầu bằng 0. Dùng chung cho hồ sơ
// cá nhân và thông tin liên hệ đơn vị tổ chức; phía trình duyệt (layout.js) chặn gõ ký tự khác số ở mọi ô type="tel".
public static class PhoneNumber
{
    public const string Message = "Số điện thoại phải gồm đúng 10 chữ số và bắt đầu bằng 0.";
    private static readonly Regex Pattern = new(@"^0\d{9}$", RegexOptions.Compiled);

    // Bỏ khoảng trắng / dấu chấm / gạch người dùng lỡ gõ rồi kiểm. Trả null nếu bỏ trống.
    public static string? Normalize(string? raw)
    {
        var digits = Regex.Replace(raw ?? "", @"[\s.\-]", "");
        return digits.Length == 0 ? null : digits;
    }

    public static bool IsValid(string? normalized) => normalized is null || Pattern.IsMatch(normalized);
}
