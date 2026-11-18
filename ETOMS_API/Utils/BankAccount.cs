using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace ETOMS_API.Utils;

// Tài khoản nhận lệ phí của giải (Tournament.BankAccountInfo). LI-02: hệ thống KHÔNG gọi cổng thanh toán,
// chỉ hiển thị cho đội chuyển khoản rồi nộp minh chứng — nên chỉ kiểm định dạng, không xác minh với ngân hàng.
// Lưu một chuỗi: "<Ngân hàng> | STK: <số> | ChuTK: <TÊN KHÔNG DẤU>".
public static class BankAccount
{
    public record Bank(string Code, string Name);

    // Ngân hàng đang hoạt động tại Việt Nam (tên viết tắt thường dùng trên app ngân hàng).
    public static readonly IReadOnlyList<Bank> Banks = new[]
    {
        new Bank("Vietcombank", "Ngân hàng TMCP Ngoại thương Việt Nam"),
        new Bank("VietinBank", "Ngân hàng TMCP Công Thương Việt Nam"),
        new Bank("BIDV", "Ngân hàng TMCP Đầu tư và Phát triển Việt Nam"),
        new Bank("Agribank", "Ngân hàng Nông nghiệp và Phát triển Nông thôn Việt Nam"),
        new Bank("Techcombank", "Ngân hàng TMCP Kỹ Thương Việt Nam"),
        new Bank("MBBank", "Ngân hàng TMCP Quân đội"),
        new Bank("ACB", "Ngân hàng TMCP Á Châu"),
        new Bank("VPBank", "Ngân hàng TMCP Việt Nam Thịnh Vượng"),
        new Bank("TPBank", "Ngân hàng TMCP Tiên Phong"),
        new Bank("Sacombank", "Ngân hàng TMCP Sài Gòn Thương Tín"),
        new Bank("HDBank", "Ngân hàng TMCP Phát triển TP. Hồ Chí Minh"),
        new Bank("VIB", "Ngân hàng TMCP Quốc tế Việt Nam"),
        new Bank("SHB", "Ngân hàng TMCP Sài Gòn – Hà Nội"),
        new Bank("SeABank", "Ngân hàng TMCP Đông Nam Á"),
        new Bank("OCB", "Ngân hàng TMCP Phương Đông"),
        new Bank("MSB", "Ngân hàng TMCP Hàng Hải Việt Nam"),
        new Bank("Eximbank", "Ngân hàng TMCP Xuất Nhập khẩu Việt Nam"),
        new Bank("LPBank", "Ngân hàng TMCP Lộc Phát Việt Nam"),
        new Bank("Nam A Bank", "Ngân hàng TMCP Nam Á"),
        new Bank("Bac A Bank", "Ngân hàng TMCP Bắc Á"),
        new Bank("ABBANK", "Ngân hàng TMCP An Bình"),
        new Bank("VietABank", "Ngân hàng TMCP Việt Á"),
        new Bank("PVcomBank", "Ngân hàng TMCP Đại Chúng Việt Nam"),
        new Bank("Saigonbank", "Ngân hàng TMCP Sài Gòn Công Thương"),
        new Bank("SCB", "Ngân hàng TMCP Sài Gòn"),
        new Bank("KienlongBank", "Ngân hàng TMCP Kiên Long"),
        new Bank("BaoViet Bank", "Ngân hàng TMCP Bảo Việt"),
        new Bank("NCB", "Ngân hàng TMCP Quốc Dân"),
        new Bank("VietBank", "Ngân hàng TMCP Việt Nam Thương Tín"),
        new Bank("PGBank", "Ngân hàng TMCP Thịnh vượng và Phát triển"),
        new Bank("BVBank", "Ngân hàng TMCP Bản Việt"),
        new Bank("GPBank", "Ngân hàng TM TNHH MTV Dầu khí Toàn cầu"),
        new Bank("Shinhan Bank", "Ngân hàng TNHH MTV Shinhan Việt Nam"),
        new Bank("Woori Bank", "Ngân hàng TNHH MTV Woori Việt Nam"),
        new Bank("HSBC", "Ngân hàng TNHH MTV HSBC Việt Nam"),
        new Bank("Standard Chartered", "Ngân hàng TNHH MTV Standard Chartered Việt Nam"),
        new Bank("UOB", "Ngân hàng TNHH MTV United Overseas Bank Việt Nam"),
        new Bank("Public Bank", "Ngân hàng TNHH MTV Public Việt Nam"),
        new Bank("CIMB", "Ngân hàng TNHH MTV CIMB Việt Nam"),
    };

    private static readonly Regex NumberPattern = new("^[0-9]{6,20}$", RegexOptions.Compiled);
    private static readonly Regex HolderPattern = new("^[A-Z]+( [A-Z]+)*$", RegexOptions.Compiled);
    private static readonly Regex StoredPattern = new(@"^(?<bank>.+?) \| STK: (?<number>[0-9]+) \| ChuTK: (?<holder>.+)$", RegexOptions.Compiled);

    // Tên chủ tài khoản như trên app ngân hàng: IN HOA, không dấu, một khoảng trắng giữa các chữ.
    public static string NormalizeHolder(string? raw)
    {
        var s = (raw ?? "").Trim().Replace('đ', 'd').Replace('Đ', 'D').Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(s.Length);
        foreach (var ch in s)
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark) sb.Append(ch);
        return Regex.Replace(sb.ToString().Normalize(NormalizationForm.FormC), @"\s+", " ").ToUpperInvariant();
    }

    public static Bank? FindBank(string? code)
    {
        var c = (code ?? "").Trim();
        return c.Length == 0 ? null : Banks.FirstOrDefault(b => string.Equals(b.Code, c, StringComparison.OrdinalIgnoreCase));
    }

    // Kiểm 3 phần và ghép chuỗi lưu. Bỏ trống cả 3 -> null (hợp lệ khi giải miễn phí).
    // Trả về null nếu hợp lệ, ngược lại là câu báo lỗi tiếng Việt.
    public static string? TryCompose(decimal entryFee, string? bank, string? number, string? holder, out string? stored)
    {
        stored = null;
        var b = (bank ?? "").Trim();
        var n = Regex.Replace(number ?? "", @"\s+", "");
        var h = NormalizeHolder(holder);
        if (b.Length == 0 && n.Length == 0 && h.Length == 0)
            return entryFee > 0 ? "Giải có lệ phí phải nhập đủ ngân hàng, số tài khoản và chủ tài khoản nhận phí." : null;
        if (b.Length == 0 || n.Length == 0 || h.Length == 0)
            return "Nhập đủ cả ngân hàng, số tài khoản và chủ tài khoản (hoặc để trống cả ba nếu giải miễn phí).";
        var found = FindBank(b);
        if (found is null) return "Vui lòng chọn ngân hàng trong danh sách.";
        if (!NumberPattern.IsMatch(n)) return "Số tài khoản chỉ gồm chữ số, dài 6–20 số.";
        if (h.Length < 2 || h.Length > 100 || !HolderPattern.IsMatch(h))
            return "Tên chủ tài khoản chỉ gồm chữ cái và khoảng trắng (2–100 ký tự), ví dụ NGUYEN VAN A.";
        stored = $"{found.Code} | STK: {n} | ChuTK: {h}";
        return null;
    }

    // Kiểm một chuỗi đã ghép sẵn (trang Sửa thông tin gửi lên).
    public static string? Validate(decimal entryFee, string? stored, out string? normalized)
    {
        normalized = null;
        var s = (stored ?? "").Trim();
        if (s.Length == 0) return TryCompose(entryFee, null, null, null, out normalized);
        var m = StoredPattern.Match(s);
        if (!m.Success) return "Vui lòng chọn ngân hàng, nhập số tài khoản và chủ tài khoản.";
        return TryCompose(entryFee, m.Groups["bank"].Value, m.Groups["number"].Value, m.Groups["holder"].Value, out normalized);
    }
}
