namespace ETOMS_API.Utils;

// Đọc định dạng + kích thước ảnh từ phần đầu tệp (không tin đuôi tệp / Content-Type do client gửi).
// Chỉ hỗ trợ PNG và JPEG — đủ cho ảnh đại diện, không cần thêm thư viện xử lý ảnh.
public static class ImageHeader
{
    public sealed record Info(string Extension, int Width, int Height);

    public static Info? Read(Stream stream)
    {
        var header = new byte[64 * 1024];
        var read = 0;
        int n;
        while (read < header.Length && (n = stream.Read(header, read, header.Length - read)) > 0) read += n;
        return ReadPng(header, read) ?? ReadJpeg(header, read);
    }

    private static Info? ReadPng(byte[] b, int len)
    {
        byte[] sig = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        if (len < 24 || !b.AsSpan(0, 8).SequenceEqual(sig)) return null;
        int w = (b[16] << 24) | (b[17] << 16) | (b[18] << 8) | b[19];
        int h = (b[20] << 24) | (b[21] << 16) | (b[22] << 8) | b[23];
        return new Info(".png", w, h);
    }

    private static Info? ReadJpeg(byte[] b, int len)
    {
        if (len < 4 || b[0] != 0xFF || b[1] != 0xD8) return null;
        var i = 2;
        while (i + 9 < len)
        {
            if (b[i] != 0xFF) { i++; continue; }
            var marker = b[i + 1];
            var segment = (b[i + 2] << 8) | b[i + 3];
            // SOF0..SOF15 (trừ DHT C4, JPG C8, DAC CC) chứa kích thước ảnh.
            if (marker is >= 0xC0 and <= 0xCF && marker != 0xC4 && marker != 0xC8 && marker != 0xCC)
                return new Info(".jpg", (b[i + 7] << 8) | b[i + 8], (b[i + 5] << 8) | b[i + 6]);
            if (segment < 2) return null;
            i += 2 + segment;
        }
        return null;
    }
}
