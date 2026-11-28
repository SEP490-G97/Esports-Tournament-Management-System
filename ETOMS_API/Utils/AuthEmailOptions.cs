namespace ETOMS_API.Utils
{
    public class EmailOptions
    {
        public const string SectionName = "Email";

        public bool Enabled { get; set; } = false;
        public string Host { get; set; } = "";
        public int Port { get; set; } = 587;
        public bool UseStartTls { get; set; } = true;
        public string User { get; set; } = "";
        public string Password { get; set; } = "";
        public string FromAddress { get; set; } = "";
        public string FromName { get; set; } = "ETOMS";

        // Gửi mail thật được khi bật và có đủ thông tin đăng nhập SMTP (mật khẩu ứng dụng thường
        // chỉ có trên máy được cấp qua user-secrets / biến môi trường, không nằm trong repo).
        public bool CanSend => Enabled && !string.IsNullOrWhiteSpace(Host)
            && (string.IsNullOrEmpty(User) || !string.IsNullOrEmpty(Password));

        // Địa chỉ nhận được thư thật: bỏ tên miền không tồn tại của dữ liệu mẫu (@demo.etoms, .test, .invalid, .local,
        // example.com) — gửi tới đó chỉ làm hộp thư gửi bị trả thư lỗi hàng loạt.
        private static readonly string[] FakeDomains = { "etoms", "test", "invalid", "local", "localhost", "example.com", "example.org" };
        public static bool IsDeliverable(string? email)
        {
            if (string.IsNullOrWhiteSpace(email)) return false;
            var at = email.LastIndexOf('@');
            if (at <= 0 || at == email.Length - 1) return false;
            var domain = email[(at + 1)..].Trim().ToLowerInvariant();
            return !FakeDomains.Any(d => domain == d || domain.EndsWith("." + d));
        }
    }

    public class AuthLinkOptions
    {
        public const string SectionName = "AuthLinks";

        public string BaseUrl { get; set; } = "";
        public int ResetMinutes { get; set; } = 30;
        public int VerifyHours { get; set; } = 24;
    }
}
