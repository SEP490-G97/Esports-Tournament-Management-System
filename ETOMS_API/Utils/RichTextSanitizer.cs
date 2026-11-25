using System.Net;
using System.Text.RegularExpressions;
using Ganss.Xss;

namespace ETOMS_API.Utils;

// Lọc HTML từ trình soạn thảo trước khi lưu. Nội dung này hiển thị công khai nên chỉ giữ thẻ
// định dạng cơ bản và liên kết http/https; script, style, ảnh và thuộc tính sự kiện bị loại.
public static class RichTextSanitizer
{
    private static readonly HtmlSanitizer Sanitizer = Create();

    private static HtmlSanitizer Create()
    {
        var s = new HtmlSanitizer();
        s.AllowedTags.Clear();
        foreach (var tag in new[] { "p", "div", "br", "h2", "h3", "b", "strong", "i", "em", "u", "ul", "ol", "li", "blockquote", "a" })
            s.AllowedTags.Add(tag);

        s.AllowedAttributes.Clear();
        s.AllowedAttributes.Add("href");

        s.AllowedSchemes.Clear();
        s.AllowedSchemes.Add("http");
        s.AllowedSchemes.Add("https");

        s.AllowedCssProperties.Clear();
        s.AllowedAtRules.Clear();
        s.KeepChildNodes = true;
        // KeepChildNodes giữ lại chữ của thẻ bị bỏ, nhưng mã trong script/style không phải nội dung.
        s.RemovingTag += (_, e) =>
        {
            if (e.Tag.NodeName is "SCRIPT" or "STYLE") e.Tag.TextContent = string.Empty;
        };
        return s;
    }

    public static string Sanitize(string html) => Sanitizer.Sanitize(html).Trim();

    private static readonly Regex HtmlTag = new(@"<\s*/?\s*(p|div|br|h[1-6]|b|strong|i|em|u|ul|ol|li|blockquote|a)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Nội dung để HIỂN THỊ bằng innerHTML (thể lệ, mô tả…): HTML từ trình soạn thảo thì lọc như trên; văn bản thường
    // (gõ tay, dữ liệu cũ) thì mã hóa ký tự rồi giữ đoạn/xuống dòng — trước đây văn bản thường bị dồn thành một khối,
    // còn HTML lại hiện nguyên thẻ <h2>, <li>… ở trang đăng ký.
    public static string? ToDisplayHtml(string? content)
    {
        if (string.IsNullOrWhiteSpace(content)) return null;
        if (HtmlTag.IsMatch(content)) return Sanitize(content);
        var paragraphs = Regex.Split(content.Replace("\r\n", "\n").Trim(), @"\n\s*\n")
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => "<p>" + WebUtility.HtmlEncode(p.Trim()).Replace("\n", "<br>") + "</p>");
        return string.Concat(paragraphs);
    }

    // Có chữ thật hay chỉ toàn thẻ rỗng (trình soạn thảo trống vẫn sinh ra <div><br></div>).
    public static bool HasText(string html)
    {
        var text = WebUtility.HtmlDecode(Regex.Replace(html, "<[^>]+>", " "));
        return !string.IsNullOrWhiteSpace(text.Replace(' ', ' '));
    }
}
