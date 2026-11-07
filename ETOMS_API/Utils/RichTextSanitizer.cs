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

    // Có chữ thật hay chỉ toàn thẻ rỗng (trình soạn thảo trống vẫn sinh ra <div><br></div>).
    public static bool HasText(string html)
    {
        var text = WebUtility.HtmlDecode(Regex.Replace(html, "<[^>]+>", " "));
        return !string.IsNullOrWhiteSpace(text.Replace(' ', ' '));
    }
}
